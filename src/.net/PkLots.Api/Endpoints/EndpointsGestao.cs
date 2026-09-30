using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsGestao
    {
        public static void MapearGestao(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/dashboard", async (ServicoDashboard servico, CancellationToken ct) =>
                    Results.Ok(await servico.ObterAsync(ct)))
                .WithTags("Dashboard")
                .RequireAuthorization(Autorizacao.Gestao)
                .WithSummary("Resumo do dia, vagas, ocupação 24h e veículos estacionados");

            app.MapGet("/api/veiculos/{placa}/historico", async (string placa, ServicoVeiculos servico, CancellationToken ct) =>
                    Results.Ok(await servico.ObterHistoricoAsync(placa, null, ct)))
                .WithTags("Veículos")
                .RequireAuthorization(Autorizacao.Gestao)
                .WithSummary("Histórico de entradas e saídas de uma placa");
        }
    }
}
