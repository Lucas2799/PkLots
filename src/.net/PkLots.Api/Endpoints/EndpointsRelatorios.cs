using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsRelatorios
    {
        // Datas no formato AAAA-MM-DD (fuso de Brasília, fim inclusivo); sem datas: mês atual até hoje
        public static void MapearRelatorios(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/relatorios/empresa", async (DateOnly? de, DateOnly? ate, Guid? patioId, ServicoRelatorios servico, CancellationToken ct) =>
                    Results.Ok(await servico.ObterDaEmpresaAsync(de, ate, patioId, ct)))
                .WithTags("Relatórios")
                .RequireAuthorization(Autorizacao.Admin)
                .WithSummary("Receita, movimento, pátios, operadores, cancelamentos e mensalistas da empresa no período");
        }
    }
}
