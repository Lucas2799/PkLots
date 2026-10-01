using System.Security.Claims;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    public static class EndpointsAdministracao
    {
        public static void MapearAdministracao(this IEndpointRouteBuilder app)
        {
            var grupo = app.MapGroup("/api/admin").WithTags("Administração").RequireAuthorization(Autorizacao.Admin);

            grupo.MapGet("/configuracao", async (ServicoAdministracao servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterConfiguracaoAsync(ct)))
                .WithSummary("Nome, valor/hora, tolerância e quantidade de vagas do pátio");

            grupo.MapPut("/configuracao", async (AtualizarConfiguracaoRequisicao requisicao, ClaimsPrincipal usuario, ServicoAdministracao servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarConfiguracaoAsync(usuario.ObterUsuarioId(), requisicao, ct)))
                .WithSummary("Altera nome, valor/hora e tolerância gratuita (minutos)");

            grupo.MapPut("/vagas/quantidade", async (AlterarQuantidadeVagasRequisicao requisicao, ClaimsPrincipal usuario, ServicoAdministracao servico, CancellationToken ct) =>
                Results.Ok(await servico.AlterarQuantidadeVagasAsync(usuario.ObterUsuarioId(), requisicao.Total, ct)))
                .WithSummary("Altera a quantidade de vagas (não remove vagas ocupadas)");

            grupo.MapGet("/patios", async (ServicoPatios servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarDaEmpresaAsync(ct)))
                .WithSummary("Pátios da empresa, inclusive desativados");

            grupo.MapPost("/patios", async (CriarPatioRequisicao requisicao, ClaimsPrincipal usuario, ServicoPatios servico, CancellationToken ct) =>
                {
                    var patio = await servico.CriarAsync(usuario.ObterUsuarioId(), requisicao, ct);
                    return Results.Created($"/api/admin/patios/{patio.Id}", patio);
                })
                .WithSummary("Cria um pátio (já com as vagas)");

            grupo.MapPut("/patios/{id:guid}", async (Guid id, AtualizarPatioRequisicao requisicao, ClaimsPrincipal usuario, ServicoPatios servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Renomeia ou ativa/desativa um pátio");

            grupo.MapGet("/impressao", async (ServicoImpressao servico, CancellationToken ct) =>
                Results.Ok(await servico.ObterConfiguracaoAsync(ct)))
                .WithSummary("Configuração de impressão do ticket de entrada");

            grupo.MapPut("/impressao", async (ConfiguracaoImpressaoDto requisicao, ClaimsPrincipal usuario, ServicoImpressao servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarConfiguracaoAsync(usuario.ObterUsuarioId(), requisicao, ct)))
                .WithSummary("Modo (desativada, rede, navegador), impressora, papel (58/80 mm) e impressão automática");

            grupo.MapPost("/impressao/teste", async (ServicoImpressao servico, CancellationToken ct) =>
                {
                    await servico.ImprimirTesteAsync(ct);
                    return Results.NoContent();
                })
                .WithSummary("Envia uma página de teste para a impressora de rede");

            grupo.MapGet("/usuarios", async (ServicoAdministracao servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarUsuariosAsync(ct)))
                .WithSummary("Lista os usuários do pátio");

            grupo.MapPost("/usuarios", async (CriarUsuarioRequisicao requisicao, ClaimsPrincipal usuario, ServicoAdministracao servico, CancellationToken ct) =>
                {
                    var criado = await servico.CriarUsuarioAsync(usuario.ObterUsuarioId(), requisicao, ct);
                    return Results.Created($"/api/admin/usuarios/{criado.Id}", criado);
                })
                .WithSummary("Cria usuário com perfil admin, operador ou motorista");

            grupo.MapPut("/usuarios/{id:guid}", async (Guid id, AtualizarUsuarioRequisicao requisicao, ClaimsPrincipal usuario, ServicoAdministracao servico, CancellationToken ct) =>
                Results.Ok(await servico.AtualizarUsuarioAsync(usuario.ObterUsuarioId(), id, requisicao, ct)))
                .WithSummary("Altera nome, perfil e situação (ativo/inativo)");

            grupo.MapPut("/usuarios/{id:guid}/senha", async (Guid id, RedefinirSenhaRequisicao requisicao, ClaimsPrincipal usuario, ServicoAdministracao servico, CancellationToken ct) =>
                {
                    await servico.RedefinirSenhaAsync(usuario.ObterUsuarioId(), id, requisicao.Senha, ct);
                    return Results.NoContent();
                })
                .WithSummary("Define uma nova senha para o usuário");
        }
    }
}
