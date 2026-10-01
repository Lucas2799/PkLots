using System.Security.Claims;
using System.Text;
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
    .WriteTo.Console()
    .WriteTo.File("logs/pklots-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Services
builder.Services.AddAplicacao();
builder.Services.AddInfraestrutura(builder.Configuration);

builder.Services.AddExceptionHandler<ManipuladorExcecoes>();
builder.Services.AddProblemDetails();

var jwt = builder.Configuration.GetSection("Jwt").Get<OpcoesJwt>() ?? new OpcoesJwt();
if (Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32)
    throw new InvalidOperationException("Jwt:SecretKey deve ter pelo menos 32 bytes.");

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
                    .Select(u => new { u.Ativo, u.Perfil, u.EmpresaId, EmpresaAtiva = u.Empresa == null || u.Empresa.Ativa })
                    .FirstOrDefaultAsync(contexto.HttpContext.RequestAborted);

                // Usuário/empresa desativados ou perfil/empresa alterados: o token deixa de valer na hora
                var empresaDoToken = contexto.Principal?.FindFirstValue(ContextoPatio.ClaimEmpresa);
                if (usuario is null || !usuario.Ativo || usuario.Perfil != perfil || !usuario.EmpresaAtiva
                    || usuario.EmpresaId?.ToString() != empresaDoToken)
                    contexto.Fail("Usuário inativo ou com perfil alterado.");
            }
        };
    });
builder.Services.AddAuthorization(Autorizacao.AdicionarPoliticas);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

app.UseCors("AllowAll");
app.UseAuthentication();

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
