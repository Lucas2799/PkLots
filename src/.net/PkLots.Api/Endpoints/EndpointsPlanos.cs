using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsPlanos
    {
        public static void MapearPlanos(this IEndpointRouteBuilder app)
        {
            // Superadmin: catálogo de planos e assinatura de cada empresa
            var plataforma = app.MapGroup("/api/plataforma").WithTags("Plataforma").RequireAuthorization(Autorizacao.SuperAdmin);

            plataforma.MapGet("/recursos", () => Results.Ok(ServicoPlanos.Recursos()))
                .WithSummary("Recursos que um plano pode liberar");

            plataforma.MapGet("/planos", async (ServicoPlanos servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(ct)))
                .WithSummary("Planos de aluguel");

            plataforma.MapPost("/planos", async (SalvarPlanoRequisicao requisicao, ServicoPlanos servico, CancellationToken ct) =>
                {
                    var plano = await servico.CriarAsync(requisicao, ct);
                    return Results.Created($"/api/plataforma/planos/{plano.Id}", plano);
                })
                .WithSummary("Cria um plano com preço, limites e recursos");

            plataforma.MapPut("/planos/{id:guid}", async (Guid id, SalvarPlanoRequisicao requisicao, ServicoPlanos servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarAsync(id, requisicao, ct)))
                .WithSummary("Altera o plano (limites e recursos valem na hora para as empresas dele)");

            plataforma.MapGet("/empresas/{id:guid}/assinatura", async (Guid id, ServicoPlanos servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterAssinaturaAsync(id, ct)))
                .WithSummary("Plano, preço e vencimento da empresa");

            plataforma.MapPut("/empresas/{id:guid}/assinatura", async (Guid id, SalvarAssinaturaRequisicao requisicao, ClaimsPrincipal usuario,
                    ServicoPlanos servico, CancellationToken ct) =>
                Results.Ok(await servico.SalvarAssinaturaAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Troca o plano, o preço negociado, o vencimento ou o período de teste");

            // Empresa: o app esconde o que o plano não inclui; o admin vê o plano e o uso
            app.MapGet("/api/assinatura/recursos", async (ServicoPlanos servico, CancellationToken ct) =>
                    Results.Ok(await servico.RecursosDaMinhaEmpresaAsync(ct)))
                .WithTags("Assinatura")
                .RequireAuthorization(Autorizacao.Gestao)
                .WithSummary("Códigos dos recursos liberados pelo plano da empresa");

            app.MapGet("/api/admin/assinatura", async (ServicoPlanos servico, CancellationToken ct) =>
                    Results.Ok(await servico.ObterMinhaAsync(ct)))
                .WithTags("Assinatura")
                .RequireAuthorization(Autorizacao.Admin)
                .WithSummary("Plano contratado, valor, vencimento, uso dos limites e recursos");
        }
    }
}
