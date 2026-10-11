using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    // Sem login: o segredo vem no link (QR do ticket). Limite de requisições por IP.
    public static class EndpointsPublicos
    {
        public const string PoliticaLimite = "publico";

        public static void MapearPublicos(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/publico").WithTags("Público").AllowAnonymous().RequireRateLimiting(PoliticaLimite);

            grupo.MapGet("/estadias/{token}", async (string token, ServicoEstadiaPublica servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterAsync(token, ct)))
                .WithSummary("Estadia do ticket (link do QR): tempo, valor parcial ou recibo");
        }
    }
}
