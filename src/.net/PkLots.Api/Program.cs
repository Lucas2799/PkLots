using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PkLots.Api.Endpoints;
using PkLots.Api.Middleware;
using PkLots.Application;
using PkLots.Application.Interfaces;
using PkLots.Infrastructure;
using PkLots.Infrastructure.Data;
using PkLots.Infrastructure.Seguranca;
using PkLots.Infrastructure.Servicos;
using Serilog;

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
    // Exceções já tratadas pelo ManipuladorExcecoes, que registra ele mesmo as falhas reais
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", Serilog.Events.LogEventLevel.Fatal)
    .Enrich.FromLogContext()
    // O healthcheck roda a cada 20 s: só polui o log
    .Filter.ByExcluding(e => e.Properties.TryGetValue("RequestPath", out var caminho) && caminho.ToString() == "\"/health\"")
    .WriteTo.Console()
    .WriteTo.File("logs/pklots-.txt", rollingInterval: RollingInterval.Day)
    // Mesmo conteúdo em JSON, lido pela tela "Logs do sistema" do superadmin (31 dias, como o texto)
    .WriteTo.File(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter(), "logs/json/pklots-.json", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Services
builder.Services.AddAplicacao();
builder.Services.AddInfraestrutura(builder.Configuration);

builder.Services.AddExceptionHandler<ManipuladorExcecoes>();
builder.Services.AddProblemDetails();

var jwt = builder.Configuration.GetSection("Jwt").Get<OpcoesJwt>() ?? new OpcoesJwt();
if (!EF.IsDesignTime)
    ValidacaoConfiguracao.Validar(builder.Configuration, builder.Environment, jwt);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Usuário desativado ou com perfil alterado perde o acesso na hora, sem esperar o token expirar
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contexto =>
            {
                var usuarioId = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var perfil = contexto.Principal?.FindFirstValue(ClaimTypes.Role);
                if (!Guid.TryParse(usuarioId, out var id))
                {
                    contexto.Fail("Token sem usuário.");
                    return;
                }

                var db = contexto.HttpContext.RequestServices.GetRequiredService<IPkLotsDbContext>();
                var usuario = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
                    .Where(u => u.Id == id)
                    .Select(u => new { u.Ativo, u.Perfil, u.EmpresaId, u.DeveTrocarSenha, EmpresaAtiva = u.Empresa == null || u.Empresa.Ativa })
                    .FirstOrDefaultAsync(contexto.HttpContext.RequestAborted);

                // Usuário/empresa desativados ou perfil/empresa alterados: o token deixa de valer na hora
                var empresaDoToken = contexto.Principal?.FindFirstValue(ContextoPatio.ClaimEmpresa);
                if (usuario is null || !usuario.Ativo || usuario.Perfil != perfil || !usuario.EmpresaAtiva
                    || usuario.EmpresaId?.ToString() != empresaDoToken)
                    contexto.Fail("Usuário inativo ou com perfil alterado.");
                else if (usuario.DeveTrocarSenha)
                    // Senha temporária/pública: só a troca de senha é permitida (ver middleware abaixo)
                    contexto.Principal!.AddIdentity(new ClaimsIdentity([new Claim(TrocaSenhaObrigatoria.Claim, "true")]));
            }
        };
    });
builder.Services.AddAuthorization(Autorizacao.AdicionarPoliticas);

// Força bruta: além do bloqueio por conta (5 erros = 15 min), limita tentativas por IP
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy("autenticacao", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Links públicos (ticket, convite): a página do ticket se atualiza sozinha, então o limite é mais folgado
    opcoes.AddPolicy(EndpointsPublicos.PoliticaLimite, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    opcoes.OnRejected = async (contexto, ct) =>
    {
        contexto.HttpContext.Response.ContentType = "application/problem+json";
        await contexto.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Muitas tentativas",
            status = 429,
            detail = "Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente."
        }, ct);
    };
});

// Atrás do Nginx: usa o IP real do cliente (X-Forwarded-For) para o limite de tentativas e os logs
builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opcoes.ForwardLimit = 1;
    opcoes.KnownNetworks.Clear();
    opcoes.KnownProxies.Clear();
    // Confia apenas no gateway, que chega por redes privadas (rede do compose)
    foreach (var rede in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8" })
        opcoes.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(rede));
});

// CORS: o app web é servido pela mesma origem (gateway) e o app mobile não usa CORS.
// Em desenvolvimento libera tudo (flutter run em outra porta); em produção só as origens configuradas.
var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Padrao", policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        else
            policy.WithOrigins(origensPermitidas).AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PkLots API", Version = "v1" });
    var esquema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", esquema);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquema] = Array.Empty<string>() });
});

var app = builder.Build();

// Comandos de administração no servidor (não sobem a API), ex.:
//   PkLots.Api criar-superadmin --email voce@empresa.com
if (args.Length > 0 && args[0] == "criar-superadmin")
{
    Environment.ExitCode = await ComandosServidor.CriarSuperAdminAsync(app.Services, args);
    return;
}

// O QR impresso aponta para este endereço: em produção ele precisa ser o domínio público (HTTPS)
var links = app.Services.GetRequiredService<PkLots.Application.Servicos.LinksPublicos>();
if (!app.Environment.IsDevelopment() && (links.UrlBase.Contains("localhost") || links.UrlBase.StartsWith("http:")))
    Log.Warning("Plataforma__UrlPublica = {Url}: os tickets e e-mails vão apontar para um endereço local ou sem HTTPS", links.UrlBase);

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

// Swagger: sempre em Development; nos demais ambientes só com Swagger:Habilitado=true
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Atrás do Nginx/containers o TLS termina no gateway; redirecionar só quando configurado
if (app.Configuration.GetValue<bool>("RedirecionarHttps"))
    app.UseHttpsRedirection();

app.UseCors("Padrao");
app.UseAuthentication();
app.UseMiddleware<TrocaSenhaObrigatoria>();
app.UseRateLimiter();
app.UseAuthorization();

// Health Check
app.MapGet("/health", () => new { status = "healthy" })
    .WithName("Health")
    .WithOpenApi()
    .AllowAnonymous();

app.MapearAutenticacao();
app.MapearMovimentacao();
app.MapearVagas();
app.MapearGestao();
app.MapearMotorista();
app.MapearAdministracao();
app.MapearPlataforma();
app.MapearMensalistas();
app.MapearCaixa();
app.MapearPublicos();
app.MapearRelatorios();
app.MapearAuditoria();
app.MapearPlanos();
app.MapearFaturas();

// Migração e dados iniciais
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PkLotsDbContext>();
    db.Database.Migrate();
    var opcoesPlataforma = builder.Configuration.GetSection("Plataforma").Get<OpcoesPlataforma>() ?? new OpcoesPlataforma();
    await SemeadorDados.SemearAsync(db, scope.ServiceProvider.GetRequiredService<IHashSenha>(), opcoesPlataforma,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SemeadorDados"));
}

app.Run();

public partial class Program;
