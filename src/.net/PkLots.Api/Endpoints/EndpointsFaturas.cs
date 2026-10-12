using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsFaturas
    {
        public static void MapearFaturas(this IEndpointRouteBuilder app)
        {
            // Superadmin: cobrança do aluguel do PkLots (baixa manual)
            var plataforma = app.MapGroup("/api/plataforma").WithTags("Plataforma").RequireAuthorization(Autorizacao.SuperAdmin);

            plataforma.MapGet("/faturas", async (Guid? empresaId, string? situacao, ServicoFaturas servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(empresaId, situacao, ct)))
                .WithSummary("Faturas das empresas (situação: aberta, vencida, paga ou cancelada)");

            plataforma.MapPost("/faturas/emitir", async (ServicoFaturas servico, CancellationToken ct) =>
                Results.Ok(await servico.EmitirMensalidadesAsync(ct)))
                .WithSummary("Emite agora as mensalidades na janela de emissão (o sistema também faz isso a cada hora)");

            plataforma.MapPost("/empresas/{id:guid}/faturas", async (Guid id, LancarFaturaRequisicao requisicao, ServicoFaturas servico, CancellationToken ct) =>
                {
                    var fatura = await servico.LancarAvulsaAsync(id, requisicao, ct);
                    return Results.Created($"/api/plataforma/faturas/{fatura.Id}", fatura);
                })
                .WithSummary("Lança uma cobrança avulsa (ex.: taxa de implantação)");

            plataforma.MapPost("/faturas/{id:guid}/pagamento", async (Guid id, BaixarFaturaRequisicao requisicao, ClaimsPrincipal usuario,
                    ServicoFaturas servico, CancellationToken ct) =>
                Results.Ok(await servico.BaixarAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Dá baixa na fatura (pagamento recebido)");

            plataforma.MapPost("/faturas/{id:guid}/cancelar", async (Guid id, CancelarFaturaRequisicao requisicao, ClaimsPrincipal usuario,
                    ServicoFaturas servico, CancellationToken ct) =>
                Results.Ok(await servico.CancelarAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Cancela uma fatura aberta, com motivo");

            plataforma.MapGet("/recebimento", async (ServicoFaturas servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterConfiguracaoAsync(ct)))
                .WithSummary("Chave PIX do PkLots usada nas faturas");

            plataforma.MapPut("/recebimento", async (ConfiguracaoPlataformaDto requisicao, ServicoFaturas servico, CancellationToken ct) =>
                Results.Ok(await servico.SalvarConfiguracaoAsync(requisicao, ct)))
                .WithSummary("Define a chave PIX do PkLots (vale para todas as faturas abertas)");

            // Gestão: o app mostra o aviso de fatura vencida ou de bloqueio parcial
            app.MapGet("/api/assinatura/situacao", async (ServicoFaturas servico, CancellationToken ct) =>
                    Results.Ok(await servico.SituacaoDaMinhaEmpresaAsync(ct)))
                .WithTags("Assinatura")
                .RequireAuthorization(Autorizacao.Gestao)
                .WithSummary("Situação de cobrança da empresa: em dia, vencida (carência) ou bloqueada");

            // Admin da empresa: as próprias faturas, com o PIX das abertas
            app.MapGet("/api/admin/faturas", async (ServicoFaturas servico, CancellationToken ct) =>
                    Results.Ok(await servico.ListarDaMinhaEmpresaAsync(ct)))
                .WithTags("Assinatura")
                .RequireAuthorization(Autorizacao.Admin)
                .WithSummary("Faturas do PkLots da empresa, com PIX copia e cola das abertas");
        }
    }
}
