using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsPlataforma
    {
        public static void MapearPlataforma(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/plataforma").WithTags("Plataforma").RequireAuthorization(Autorizacao.SuperAdmin);

            grupo.MapGet("/empresas", async (ServicoPlataforma servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarEmpresasAsync(ct)))
                .WithSummary("Empresas clientes da plataforma");

            grupo.MapPost("/empresas", async (CriarEmpresaRequisicao requisicao, ClaimsPrincipal usuario, ServicoPlataforma servico, CancellationToken ct) =>
                {
                    var empresa = await servico.CriarEmpresaAsync(usuario.ObterUsuarioId(), requisicao, ct);
                    return Results.Created($"/api/plataforma/empresas/{empresa.Id}", empresa);
                })
                .WithSummary("Cria empresa com o primeiro pátio e o administrador");

            grupo.MapPut("/empresas/{id:guid}", async (Guid id, AtualizarEmpresaRequisicao requisicao, ClaimsPrincipal usuario, ServicoPlataforma servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarEmpresaAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Renomeia ou ativa/desativa a empresa (desativada: usuários perdem o acesso na hora)");

            grupo.MapPost("/empresas/{id:guid}/convite", async (Guid id, ClaimsPrincipal usuario, ServicoPlataforma servico, CancellationToken ct) =>
                Results.Ok(await servico.ReenviarConvitesAsync(usuario.ObterUsuarioId(), id, ct)))
                .WithSummary("Reenvia o convite aos administradores que ainda não criaram a senha");
        }
    }
}
