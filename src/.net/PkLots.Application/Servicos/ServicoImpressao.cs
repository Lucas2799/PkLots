using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Impressao;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Ticket de entrada: configuração (admin), impressão na rede e dados para o PDF do navegador
    public class ServicoImpressao(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        IImpressoraTickets impressora,
        LinksPublicos links,
        TimeProvider relogio,
        ILogger<ServicoImpressao> logger)
    {
        public static readonly int[] LargurasPapel = [58, 80];

        // ---------- Configuração (admin) ----------

        public async Task<ConfiguracaoImpressaoDto> ObterConfiguracaoAsync(CancellationToken ct = default) =>
            ParaDto(await ObterPatioAsync(ct));

        public async Task<ConfiguracaoImpressaoDto> AtualizarConfiguracaoAsync(Guid adminId, ConfiguracaoImpressaoDto requisicao, CancellationToken ct = default)
        {
            var modo = (requisicao.Modo ?? "").Trim().ToLowerInvariant();
            if (!ModoImpressao.Todos.Contains(modo))
                throw new ExcecaoNegocio($"Modo de impressão inválido. Use: {string.Join(", ", ModoImpressao.Todos)}.");
            if (!LargurasPapel.Contains(requisicao.LarguraPapelMm))
                throw new ExcecaoNegocio("A largura do papel deve ser 58 ou 80 mm.");
            if (requisicao.ImpressoraPorta is < 1 or > 65535)
                throw new ExcecaoNegocio("Porta inválida (1 a 65535).");

            var host = string.IsNullOrWhiteSpace(requisicao.ImpressoraHost) ? null : requisicao.ImpressoraHost.Trim();
            if (modo == ModoImpressao.Rede && (host is null || Uri.CheckHostName(host) == UriHostNameType.Unknown))
                throw new ExcecaoNegocio("Informe o IP ou nome da impressora de rede (ex.: 192.168.0.50).");

            var patio = await ObterPatioAsync(ct);
            if (modo == ModoImpressao.Rede && patio.ModoImpressao != ModoImpressao.Rede)
                await RegrasPlano.ExigirRecursoAsync(db, patio.EmpresaId, RecursosPlano.ImpressoraRede, ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.ConfiguracaoImpressaoAlterada, SituacaoAuditoria.Sucesso, null, agora,
                new()
                {
                    ["adminId"] = adminId.ToString(),
                    ["modoAnterior"] = patio.ModoImpressao,
                    ["modo"] = modo,
                    ["impressora"] = host is null ? "" : $"{host}:{requisicao.ImpressoraPorta}",
                    ["larguraPapelMm"] = requisicao.LarguraPapelMm,
                    ["automatica"] = requisicao.ImprimirEntradaAutomaticamente
                });

            patio.ModoImpressao = modo;
            patio.ImpressoraHost = host;
            patio.ImpressoraPorta = requisicao.ImpressoraPorta;
            patio.LarguraPapelMm = requisicao.LarguraPapelMm;
            patio.ImprimirEntradaAutomaticamente = requisicao.ImprimirEntradaAutomaticamente;
            patio.AtualizadoEm = agora;

            await db.SaveChangesAsync(ct);
            return ParaDto(patio);
        }

        public async Task ImprimirTesteAsync(CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            ExigirImpressoraDeRede(patio);
            await EnviarAsync(patio, GeradorTicketEscPos.Teste(patio.LarguraPapelMm, relogio.GetUtcNow().UtcDateTime), ct);
        }

        // ---------- Ticket de entrada ----------

        public async Task<TicketEntradaDto> ObterTicketEntradaAsync(Guid entradaId, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var dados = await db.EntradasVeiculos
                .Where(e => e.Id == entradaId && e.PatioId == patioId)
                .Select(e => new
                {
                    e.Id,
                    e.CodigoTicket,
                    e.TokenAcesso,
                    e.Placa,
                    e.DataHoraEntrada,
                    NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null,
                    e.Patio.Nome,
                    e.Patio.ValorHora,
                    e.Patio.MinutosTolerancia,
                    e.Patio.LarguraPapelMm
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new ExcecaoNaoEncontrado("Entrada não encontrada.");

            // Entradas anteriores ao link do ticket ganham o token na primeira reimpressão
            var token = dados.TokenAcesso;
            if (token is null)
            {
                var entrada = await db.EntradasVeiculos.FirstAsync(e => e.Id == dados.Id, ct);
                token = entrada.TokenAcesso ??= TokenSeguro.Gerar(TokenSeguro.BytesTicket);
                await db.SaveChangesAsync(ct);
            }

            return new TicketEntradaDto(
                dados.Id,
                CodigoTicket.Formatar(dados.CodigoTicket ?? Codigo(dados.Id)),
                dados.Nome,
                dados.Placa,
                dados.NumeroVaga,
                dados.DataHoraEntrada,
                dados.ValorHora,
                dados.MinutosTolerancia,
                dados.LarguraPapelMm,
                links.Ticket(token));
        }

        // Chamado após registrar a entrada. Falha na impressora nunca desfaz a entrada.
        public async Task<string?> AposEntradaAsync(Guid entradaId, CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            if (!patio.ImprimirEntradaAutomaticamente || patio.ModoImpressao == ModoImpressao.Desativada)
                return null;
            if (patio.ModoImpressao == ModoImpressao.Navegador)
                return ResultadoImpressao.Navegador;

            try
            {
                await ImprimirEntradaAsync(entradaId, ct);
                return ResultadoImpressao.Impresso;
            }
            catch (ExcecaoServicoExterno ex)
            {
                var ticket = await ObterTicketEntradaAsync(entradaId, ct);
                Auditoria.Registrar(db, patio.Id, AcoesAuditoria.ImpressaoFalhou, SituacaoAuditoria.Alerta, ticket.Placa,
                    relogio.GetUtcNow().UtcDateTime,
                    new() { ["entradaId"] = entradaId.ToString(), ["erro"] = ex.Message });
                await db.SaveChangesAsync(ct);
                return ResultadoImpressao.Falhou;
            }
        }

        public async Task ImprimirEntradaAsync(Guid entradaId, CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            ExigirImpressoraDeRede(patio);
            var ticket = await ObterTicketEntradaAsync(entradaId, ct);
            await EnviarAsync(patio, GeradorTicketEscPos.TicketEntrada(ticket), ct);
        }

        // ---------- Auxiliares ----------

        // Entradas anteriores ao código de ticket: os 8 primeiros caracteres do Id (o que os tickets antigos mostravam)
        public static string Codigo(Guid entradaId) => entradaId.ToString("N")[..8].ToUpperInvariant();

        private static void ExigirImpressoraDeRede(Patio patio)
        {
            if (patio.ModoImpressao != ModoImpressao.Rede || string.IsNullOrWhiteSpace(patio.ImpressoraHost))
                throw new ExcecaoConflito("Nenhuma impressora de rede configurada. Configure em Administração → Impressão.");
        }

        private async Task EnviarAsync(Patio patio, byte[] dados, CancellationToken ct)
        {
            try
            {
                await impressora.EnviarAsync(patio.ImpressoraHost!, patio.ImpressoraPorta, dados, ct);
            }
            catch (ExcecaoServicoExterno)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Falha ao imprimir em {Host}:{Porta}", patio.ImpressoraHost, patio.ImpressoraPorta);
                throw new ExcecaoServicoExterno(
                    $"Não foi possível imprimir na impressora {patio.ImpressoraHost}:{patio.ImpressoraPorta}. Verifique se está ligada e na rede.", ex);
            }
        }

        private async Task<Patio> ObterPatioAsync(CancellationToken ct)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            return await db.Patios.FirstOrDefaultAsync(p => p.Id == patioId, ct)
                ?? throw new ExcecaoNaoEncontrado("Pátio não encontrado.");
        }

        private static ConfiguracaoImpressaoDto ParaDto(Patio p) =>
            new(p.ModoImpressao, p.ImpressoraHost, p.ImpressoraPorta, p.LarguraPapelMm, p.ImprimirEntradaAutomaticamente);
    }
}
