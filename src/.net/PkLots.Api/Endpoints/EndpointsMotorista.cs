using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsMotorista
    {
        public static void MapearMotorista(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/motorista").WithTags("Motorista").RequireAuthorization(Autorizacao.Motorista);

            grupo.MapGet("/veiculos", async (ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarVeiculosAsync(usuario.ObterUsuarioId(), ct)))
                .WithSummary("Veículos vinculados ao motorista");

            grupo.MapPost("/veiculos", async (CadastrarVeiculoRequisicao requisicao, ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.CadastrarVeiculoAsync(usuario.ObterUsuarioId(), requisicao, ct)))
                .WithSummary("Vincula uma placa ao motorista");

            grupo.MapGet("/estacionamento-atual", async (ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                {
                    var atual = await servico.ObterEstacionamentoAtualAsync(usuario.ObterUsuarioId(), ct);
                    return atual is null ? Results.NoContent() : Results.Ok(atual);
                })
                .WithSummary("Veículo estacionado agora, com duração e valor estimado");

            grupo.MapGet("/historico", async (ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterHistoricoAsync(usuario.ObterUsuarioId(), ct)))
                .WithSummary("Acessos dos últimos 30 dias");

            grupo.MapPost("/iniciar-saida", async (ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.IniciarSaidaAsync(usuario.ObterUsuarioId(), ct)))
                .WithSummary("Registra a saída do veículo estacionado e calcula o valor");
        }
    }
}
