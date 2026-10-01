using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsCaixa
    {
        public static void MapearCaixa(this IEndpointRouteBuilder app)
        {
            // Cada operador (ou admin) tem o próprio caixa, aberto no pátio selecionado
            var grupo = app.MapGroup("/api/caixa").WithTags("Caixa").RequireAuthorization(Autorizacao.Gestao);

            grupo.MapGet("/atual", async (ServicoCaixa servico, CancellationToken ct) =>
                    await servico.ObterAtualAsync(ct) is { } caixa ? Results.Ok(caixa) : Results.NoContent())
                .WithSummary("Caixa aberto do usuário (204 se não houver)");

            grupo.MapPost("/abrir", async (AbrirCaixaRequisicao requisicao, ServicoCaixa servico, CancellationToken ct) =>
                Results.Ok(await servico.AbrirAsync(requisicao, ct)))
                .WithSummary("Abre o caixa com o troco inicial");

            grupo.MapPost("/movimentos", async (MovimentarCaixaRequisicao requisicao, ServicoCaixa servico, CancellationToken ct) =>
                Results.Ok(await servico.MovimentarAsync(requisicao, ct)))
                .WithSummary("Sangria (retirada) ou suprimento (reforço de troco)");

            grupo.MapPost("/fechar", async (FecharCaixaRequisicao requisicao, ServicoCaixa servico, CancellationToken ct) =>
                Results.Ok(await servico.FecharAsync(requisicao, ct)))
                .WithSummary("Fecha o caixa informando o dinheiro contado (calcula a diferença)");

            grupo.MapGet("/", async (DateOnly? data, ServicoCaixa servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(data, ct)))
                .WithSummary("Caixas do dia no pátio (admin vê todos os operadores)");
        }
    }
}
