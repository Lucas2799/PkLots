using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsMovimentacao
    {
        public static void MapearMovimentacao(this IEndpointRouteBuilder app)
        {
            var entradas = app.MapGroup("/api/entradas").WithTags("Entradas").RequireAuthorization(Autorizacao.Gestao);

            entradas.MapPost("/", async (RegistrarEntradaRequisicao requisicao, ServicoEntrada servico, ServicoImpressao impressao, CancellationToken ct) =>
                {
                    var entrada = await servico.RegistrarAsync(requisicao, ct);
                    entrada = entrada with { Impressao = await impressao.AposEntradaAsync(entrada.Id, ct) };
                    return Results.Created($"/api/entradas/{entrada.Id}", entrada);
                })
                .WithSummary("Registra entrada com placa já reconhecida (imprime o ticket se configurado)");

            entradas.MapPost("/imagem", async (IFormFile imagem, ServicoEntrada servico, ServicoImpressao impressao, CancellationToken ct) =>
                {
                    await using var fluxo = imagem.OpenReadStream();
                    var entrada = await servico.RegistrarPorImagemAsync(fluxo, imagem.FileName, ct);
                    entrada = entrada with { Impressao = await impressao.AposEntradaAsync(entrada.Id, ct) };
                    return Results.Created($"/api/entradas/{entrada.Id}", entrada);
                })
                .DisableAntiforgery()
                .WithSummary("Registra entrada a partir da foto da câmera (LPR)");

            entradas.MapGet("/{id:guid}/ticket", async (Guid id, ServicoImpressao impressao, CancellationToken ct) =>
                    Results.Ok(await impressao.ObterTicketEntradaAsync(id, ct)))
                .WithSummary("Dados do ticket de entrada (para gerar o PDF no navegador)");

            entradas.MapPost("/{id:guid}/imprimir", async (Guid id, ServicoImpressao impressao, CancellationToken ct) =>
                {
                    await impressao.ImprimirEntradaAsync(id, ct);
                    return Results.NoContent();
                })
                .WithSummary("Imprime (ou reimprime) o ticket na impressora de rede");

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
