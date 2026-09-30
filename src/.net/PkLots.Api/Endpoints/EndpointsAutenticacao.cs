using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsAutenticacao
    {
        public static void MapearAutenticacao(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/auth").WithTags("Autenticação").AllowAnonymous();

            grupo.MapPost("/login", async (LoginRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                Results.Ok(await servico.LoginAsync(requisicao, ct)))
                .WithSummary("Autentica e retorna o token JWT (24h)");

            grupo.MapPost("/registrar", async (RegistroRequisicao requisicao, ServicoAutenticacao servico, CancellationToken ct) =>
                Results.Ok(await servico.RegistrarMotoristaAsync(requisicao, ct)))
                .WithSummary("Cria conta de motorista e retorna o token JWT");
        }
    }
}
