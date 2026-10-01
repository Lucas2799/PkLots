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

            grupo.MapPost("/registrar", async (RegistroRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                Results.Ok(await servico.RegistrarMotoristaAsync(requisicao, ct)))
                .AllowAnonymous()
                .WithSummary("Cria conta de motorista e retorna o token JWT");

            grupo.MapPost("/trocar-senha", async (TrocarSenhaRequisicao requisicao, ClaimsPrincipal usuario, ServicoAutenticacao servico,
                    CancellationToken ct) =>
                Results.Ok(await servico.TrocarSenhaAsync(usuario.ObterUsuarioId(), requisicao, ct)))
                .RequireAuthorization()
                .WithSummary("Troca a própria senha (obrigatória quando a senha é temporária) e retorna novo token");
        }
    }
}
