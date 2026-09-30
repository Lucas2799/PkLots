using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsVagas
    {
        public static void MapearVagas(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/vagas").WithTags("Vagas").RequireAuthorization();

            grupo.MapGet("/", async (ServicoVagas servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(ct)))
                .WithSummary("Lista todas as vagas do pátio");

            grupo.MapGet("/resumo", async (ServicoVagas servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterResumoAsync(ct)))
                .WithSummary("Total de vagas disponíveis, ocupadas e em manutenção");

            grupo.MapPut("/{id:guid}/manutencao", async (Guid id, ManutencaoRequisicao? requisicao, ServicoVagas servico, CancellationToken ct) =>
                Results.Ok(await servico.MarcarManutencaoAsync(id, requisicao?.Observacoes, ct)))
                .RequireAuthorization(Autorizacao.Gestao)
                .WithSummary("Marca a vaga como em manutenção");

            grupo.MapDelete("/{id:guid}/manutencao", async (Guid id, ServicoVagas servico, CancellationToken ct) =>
                Results.Ok(await servico.LiberarManutencaoAsync(id, ct)))
                .RequireAuthorization(Autorizacao.Gestao)
                .WithSummary("Retira a vaga da manutenção");
        }
    }
}
