using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    // Sem login: o segredo vem no link (QR do ticket ou e-mail). Limite de requisições por IP.
    public static class EndpointsPublicos
    {
        public const string PoliticaLimite = "publico";

        public static void MapearPublicos(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/publico").WithTags("Público").AllowAnonymous().RequireRateLimiting(PoliticaLimite);

            grupo.MapGet("/estadias/{token}", async (string token, ServicoEstadiaPublica servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterAsync(token, ct)))
                .WithSummary("Estadia do ticket (link do QR): tempo, valor parcial ou recibo");

            grupo.MapGet("/convites/{token}", async (string token, ServicoAcessoMensalista servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterConviteAsync(token, ct)))
                .WithSummary("Dados do convite de mensalista (placas mascaradas) antes de aceitar");

            grupo.MapPost("/convites/{token}/aceitar", async (string token, AceitarConviteRequisicao requisicao,
                    ServicoAcessoMensalista servico, CancellationToken ct) =>
                Results.Ok(await servico.AceitarConviteAsync(token, requisicao, ct)))
                .WithSummary("Aceita o convite: cria a conta (ou usa a existente) e vincula ao contrato");
        }
    }
}
