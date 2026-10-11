using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsAutenticacao
    {
        public static void MapearAutenticacao(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/auth").WithTags("Autenticação").RequireRateLimiting("autenticacao");

            grupo.MapPost("/login", async (LoginRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                Results.Ok(await servico.LoginAsync(requisicao, ct)))
                .AllowAnonymous()
                .WithSummary("Autentica e retorna o token JWT (24h)");

            grupo.MapPost("/esqueci-senha", async (EsqueciSenhaRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                {
                    await servico.SolicitarRedefinicaoAsync(requisicao, ct);
                    return Results.NoContent();
                })
                .AllowAnonymous()
                .WithSummary("Envia o link de redefinição de senha (mesma resposta exista ou não a conta)");

            grupo.MapPost("/redefinir-senha", async (RedefinirSenhaPorEmailRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                {
                    await servico.RedefinirSenhaAsync(requisicao, ct);
                    return Results.NoContent();
                })
                .AllowAnonymous()
                .WithSummary("Define a nova senha com o token recebido por e-mail");

            grupo.MapPost("/ativar-conta", async (AtivarContaRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                Results.Ok(await servico.AtivarContaAsync(requisicao, ct)))
                .AllowAnonymous()
                .WithSummary("Cria a primeira senha pelo convite (admin/operador) e retorna o token");

            grupo.MapPost("/trocar-senha", async (TrocarSenhaRequisicao requisicao, ClaimsPrincipal usuario, ServicoAutenticacao servico,
                    CancellationToken ct) =>
                Results.Ok(await servico.TrocarSenhaAsync(usuario.ObterUsuarioId(), requisicao, ct)))
                .RequireAuthorization()
                .WithSummary("Troca a própria senha (obrigatória quando a senha é temporária) e retorna novo token");
        }
    }
}
