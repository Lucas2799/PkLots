using System.Security.Claims;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    // App do mensalista (perfil motorista): só os contratos em que a pessoa aceitou o convite do pátio
    public static class EndpointsMotorista
    {
        public static void MapearMotorista(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/motorista").WithTags("Mensalista (app)").RequireAuthorization(Autorizacao.Motorista);

            grupo.MapGet("/contratos", async (ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarContratosAsync(usuario.ObterUsuarioId(), ct)))
                .WithSummary("Contratos de mensalista da conta, com validade e veículos estacionados agora");

            grupo.MapGet("/historico", async (ClaimsPrincipal usuario, ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterHistoricoAsync(usuario.ObterUsuarioId(), ct)))
                .WithSummary("Estadias dos últimos 30 dias nos pátios dos contratos (a partir do vínculo)");

            grupo.MapGet("/patios", async (ServicoMotorista servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarPatiosAsync(ct)))
                .WithSummary("Pátios disponíveis e vagas livres agora");
        }
    }
}
