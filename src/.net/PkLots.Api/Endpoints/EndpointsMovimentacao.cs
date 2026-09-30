using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsMovimentacao
    {
        public static void MapearMovimentacao(this IEndpointRouteBuilder app)
        {
            var entradas = app.MapGroup("/api/entradas").WithTags("Entradas").RequireAuthorization(Autorizacao.Gestao);

            entradas.MapPost("/", async (RegistrarEntradaRequisicao requisicao, ServicoEntrada servico, CancellationToken ct) =>
                {
                    var entrada = await servico.RegistrarAsync(requisicao, ct);
                    return Results.Created($"/api/entradas/{entrada.Id}", entrada);
                })
                .WithSummary("Registra entrada com placa já reconhecida");

            entradas.MapPost("/imagem", async (IFormFile imagem, ServicoEntrada servico, CancellationToken ct) =>
                {
                    await using var fluxo = imagem.OpenReadStream();
                    var entrada = await servico.RegistrarPorImagemAsync(fluxo, imagem.FileName, ct);
                    return Results.Created($"/api/entradas/{entrada.Id}", entrada);
                })
                .DisableAntiforgery()
                .WithSummary("Registra entrada a partir da foto da câmera (LPR)");

            var saidas = app.MapGroup("/api/saidas").WithTags("Saídas").RequireAuthorization(Autorizacao.Gestao);

            saidas.MapPost("/", async (RegistrarSaidaRequisicao requisicao, ServicoSaida servico, CancellationToken ct) =>
                    Results.Ok(await servico.RegistrarAsync(requisicao, ct)))
                .WithSummary("Registra saída e calcula o valor");

            saidas.MapPost("/imagem", async (IFormFile imagem, ServicoSaida servico, CancellationToken ct) =>
                {
                    await using var fluxo = imagem.OpenReadStream();
                    return Results.Ok(await servico.RegistrarPorImagemAsync(fluxo, imagem.FileName, ct));
                })
                .DisableAntiforgery()
                .WithSummary("Registra saída a partir da foto da câmera (LPR)");
        }
    }
}
