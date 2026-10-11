using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsMensalistas
    {
        public static void MapearMensalistas(this IEndpointRouteBuilder app)
        {
            // Admin e operador, sempre no pátio selecionado (X-Patio-Id)
            var grupo = app.MapGroup("/api/mensalistas").WithTags("Mensalistas").RequireAuthorization(Autorizacao.Gestao);

            grupo.MapGet("/", async (string? situacao, string? busca, ServicoMensalistas servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(situacao, busca, ct)))
                .WithSummary("Lista mensalistas (filtro: em_dia, vencendo, vencido, inativo; busca por nome ou placa)");

            grupo.MapGet("/{id:guid}", async (Guid id, ServicoMensalistas servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterAsync(id, ct)));

            grupo.MapPost("/", async (SalvarMensalistaRequisicao requisicao, ClaimsPrincipal usuario, ServicoMensalistas servico, CancellationToken ct) =>
                {
                    var mensalista = await servico.CriarAsync(usuario.ObterUsuarioId(), requisicao, ct);
                    return Results.Created($"/api/mensalistas/{mensalista.Id}", mensalista);
                })
                .WithSummary("Cadastra mensalista com uma ou mais placas");

            grupo.MapPut("/{id:guid}", async (Guid id, SalvarMensalistaRequisicao requisicao, ClaimsPrincipal usuario, ServicoMensalistas servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Altera dados, placas ou ativa/desativa");

            grupo.MapPost("/{id:guid}/convite", async (Guid id, ClaimsPrincipal usuario, ServicoMensalistas servico, CancellationToken ct) =>
                Results.Ok(await servico.ReenviarConviteAsync(usuario.ObterUsuarioId(), id, ct)))
                .WithSummary("Reenvia o convite de acesso ao app para o e-mail do cadastro");

            grupo.MapGet("/{id:guid}/pagamentos", async (Guid id, ServicoMensalistas servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarPagamentosAsync(id, ct)))
                .WithSummary("Histórico de pagamentos");

            grupo.MapPost("/{id:guid}/pagamentos", async (Guid id, RegistrarPagamentoRequisicao requisicao, ClaimsPrincipal usuario, ServicoMensalistas servico, CancellationToken ct) =>
                Results.Ok(await servico.RegistrarPagamentoAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Registra pagamento (dinheiro, pix, cartão...) e estende a validade");
        }
    }
}
