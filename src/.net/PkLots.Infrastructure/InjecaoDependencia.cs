using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;
using PkLots.Infrastructure.Email;
using PkLots.Infrastructure.Data;
using PkLots.Infrastructure.Seguranca;
using PkLots.Infrastructure.Servicos;
using StackExchange.Redis;

namespace PkLots.Infrastructure
{
    public static class InjecaoDependencia
    {
        public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuracao)
        {
            services.AddDbContext<PkLotsDbContext>(options =>
                options.UseNpgsql(configuracao.GetConnectionString("DefaultConnection")));
            services.AddScoped<IPkLotsDbContext>(sp => sp.GetRequiredService<PkLotsDbContext>());

            services.AddHttpContextAccessor();
            services.AddScoped<IContextoPatio, ContextoPatio>();

            // Redis (opcional): sem connection string, o resumo de vagas vem sempre do banco
            var redis = configuracao.GetConnectionString("RedisConnection");
            if (string.IsNullOrWhiteSpace(redis))
            {
                services.AddSingleton<ICacheVagas, CacheVagasNulo>();
            }
            else
            {
                var opcoesRedis = ConfigurationOptions.Parse(redis);
                opcoesRedis.AbortOnConnectFail = false;
                opcoesRedis.ConnectTimeout = 2000;
                opcoesRedis.SyncTimeout = 1000;
                opcoesRedis.AsyncTimeout = 1000;
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(opcoesRedis));
                services.AddSingleton<ICacheVagas, CacheVagasRedis>();
            }

            services.AddHttpClient<IServicoLpr, ServicoLprHttp>(cliente =>
            {
                var url = configuracao["Services:LprServiceUrl"] ?? "http://localhost:5000";
                cliente.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                cliente.Timeout = TimeSpan.FromSeconds(15);
            });

            services.Configure<OpcoesJwt>(configuracao.GetSection("Jwt"));
            services.AddSingleton<IGeradorToken, GeradorTokenJwt>();
            services.AddSingleton<IHashSenha, HashSenhaBcrypt>();
            services.AddSingleton<IImpressoraTickets, ImpressoraTicketsTcp>();

            // Endereço público do gateway: vai no QR do ticket e nos links dos e-mails
            services.AddSingleton(new LinksPublicos(configuracao["Plataforma:UrlPublica"] ?? "http://localhost:8000"));

            services.Configure<OpcoesEmail>(configuracao.GetSection("Email"));
            services.AddScoped<EnviadorEmailSmtp>();
            services.AddHostedService<ProcessadorFilaEmails>();
            services.AddHostedService<ProcessadorFaturas>();

            return services;
        }
    }
}
