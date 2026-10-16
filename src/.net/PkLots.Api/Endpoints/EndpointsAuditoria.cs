using PkLots.Application.DTOs;
using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsAuditoria
    {
        // Datas no formato AAAA-MM-DD (fuso de Brasília, fim inclusivo)
        public static void MapearAuditoria(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/auditoria").WithTags("Auditoria").RequireAuthorization(Autorizacao.Auditoria);

            grupo.MapGet("/", async (DateOnly? de, DateOnly? ate, Guid? empresaId, Guid? patioId, string? categoria, string? situacao,
                    string? placa, Guid? usuarioId, int? pagina, int? tamanhoPagina, ServicoAuditoria servico, CancellationToken ct) =>
                    Results.Ok(await servico.ListarAsync(new FiltroAuditoria(de, ate, empresaId, patioId, categoria, situacao, placa, usuarioId,
                        pagina ?? 1, tamanhoPagina ?? 50), ct)))
                .WithSummary("Quem fez o quê e quando (admin: a própria empresa; superadmin: todas ou a escolhida), mais recentes primeiro");

            grupo.MapGet("/opcoes", async (Guid? empresaId, ServicoAuditoria servico, CancellationToken ct) =>
                    Results.Ok(await servico.OpcoesAsync(empresaId, ct)))
                .WithSummary("Categorias, pátios, usuários e empresas para os filtros da auditoria");

            app.MapGet("/api/plataforma/logs", async (DateOnly? de, DateOnly? ate, string? nivel, string? texto, int? pagina, int? tamanhoPagina,
                    ILeitorLogsSistema leitor, CancellationToken ct) =>
                    Results.Ok(await leitor.LerAsync(new FiltroLogsSistema(de, ate, nivel, texto, pagina ?? 1, tamanhoPagina ?? 100), ct)))
                .WithTags("Plataforma")
                .RequireAuthorization(Autorizacao.SuperAdmin)
                .WithSummary("Log técnico da API (requisições, avisos e erros), mais recentes primeiro; até 7 dias por consulta");
        }
    }
}
