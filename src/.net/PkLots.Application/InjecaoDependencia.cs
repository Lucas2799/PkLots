using Microsoft.Extensions.DependencyInjection;
using PkLots.Application.Servicos;

namespace PkLots.Application
{
    public static class InjecaoDependencia
    {
        public static IServiceCollection AddAplicacao(this IServiceCollection services)
        {
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<ServicoAutenticacao>();
            services.AddScoped<ReconhecedorPlaca>();
            services.AddScoped<ServicoEntrada>();
            services.AddScoped<ServicoSaida>();
            services.AddScoped<ServicoVagas>();
            services.AddScoped<ServicoDashboard>();
            services.AddScoped<ServicoVeiculos>();
            services.AddScoped<ServicoMotorista>();
            services.AddScoped<ServicoAdministracao>();
            services.AddScoped<ServicoImpressao>();
            services.AddScoped<ServicoPatios>();
            services.AddScoped<ServicoPlataforma>();
            services.AddScoped<ServicoMensalistas>();
            services.AddScoped<ServicoConfiguracaoPagamento>();
            services.AddScoped<ServicoCaixa>();
            services.AddScoped<ServicoEstadiaPublica>();
            services.AddScoped<ServicoAcessoMensalista>();
            services.AddScoped<ServicoRelatorios>();
            services.AddScoped<ServicoPlanos>();
            services.AddScoped<ServicoFaturas>();
            services.AddScoped<ServicoTabelaPreco>();
            return services;
        }
    }
}
