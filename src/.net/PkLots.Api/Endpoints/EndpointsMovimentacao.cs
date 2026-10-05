using System.Security.Claims;
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

            entradas.MapPost("/{id:guid}/cancelar", async (Guid id, CancelarEntradaRequisicao requisicao, ServicoEntrada servico, CancellationToken ct) =>
                {
                    await servico.CancelarAsync(id, requisicao, ct);
                    return Results.NoContent();
                })
                .WithSummary("Cancela uma entrada ativa com motivo (operador: só logo após a entrada; admin: a qualquer momento)");

            var saidas = app.MapGroup("/api/saidas").WithTags("Saídas").RequireAuthorization(Autorizacao.Gestao);

            // 1) Identifica o veículo e calcula a cobrança (valor congelado por alguns minutos; PIX gerado com a chave do pátio)
            saidas.MapPost("/cobranca", async (GerarCobrancaRequisicao requisicao, ServicoSaida servico, CancellationToken ct) =>
                    Results.Ok(await servico.CobrarAsync(requisicao, ct)))
                .WithSummary("Calcula a cobrança da saída pela placa ou pelo código do ticket");

            saidas.MapPost("/cobranca/imagem", async (IFormFile imagem, ServicoSaida servico, CancellationToken ct) =>
                {
                    await using var fluxo = imagem.OpenReadStream();
                    return Results.Ok(await servico.CobrarPorImagemAsync(fluxo, imagem.FileName, ct));
                })
                .DisableAntiforgery()
                .WithSummary("Calcula a cobrança a partir da foto da placa (LPR)");

            // 2) Confirma o pagamento e registra a saída (exige caixa aberto quando há valor)
            saidas.MapPost("/cobranca/{id:guid}/confirmar", async (Guid id, ConfirmarSaidaRequisicao requisicao, ClaimsPrincipal usuario,
                    ServicoSaida servico, CancellationToken ct) =>
                    Results.Ok(await servico.ConfirmarAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Confirma o pagamento (dinheiro, pix, cartão) e libera a saída");
        }
    }
}
