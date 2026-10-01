using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Application.Pagamentos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Como a empresa recebe (provedor, chave PIX, formas aceitas). Contrato com o provedor é do dono do pátio.
    public class ServicoConfiguracaoPagamento(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        // Configuração da empresa ou o padrão (dinheiro e cartão; PIX só após cadastrar a chave)
        public static async Task<ConfiguracaoPagamento> ObterDaEmpresaAsync(IPkLotsDbContext db, Guid empresaId, CancellationToken ct) =>
            await db.ConfiguracoesPagamento.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct)
            ?? new ConfiguracaoPagamento { EmpresaId = empresaId };

        // Formas realmente utilizáveis na saída (PIX exige chave cadastrada)
        public static IReadOnlyList<string> FormasAceitas(ConfiguracaoPagamento c) =>
        [
            .. c.AceitaDinheiro ? [FormasPagamento.Dinheiro] : Array.Empty<string>(),
            .. c.AceitaPix && c.ChavePix is not null ? [FormasPagamento.Pix] : Array.Empty<string>(),
            .. c.AceitaCartao ? [FormasPagamento.Cartao] : Array.Empty<string>(),
        ];

        public async Task<ConfiguracaoPagamentoDto> ObterAsync(CancellationToken ct = default) =>
            ParaDto(await ObterDaEmpresaAsync(db, ObterEmpresaId(), ct));

        public async Task<ConfiguracaoPagamentoDto> AtualizarAsync(Guid adminId, AtualizarConfiguracaoPagamentoRequisicao requisicao, CancellationToken ct = default)
        {
            var provedor = (requisicao.Provedor ?? "").Trim().ToLowerInvariant();
            if (!ProvedoresPagamento.Todos.Contains(provedor))
                throw new ExcecaoNegocio($"Provedor de pagamento inválido. Disponíveis: {string.Join(", ", ProvedoresPagamento.Todos)}.");
            if (!requisicao.AceitaDinheiro && !requisicao.AceitaPix && !requisicao.AceitaCartao)
                throw new ExcecaoNegocio("Habilite pelo menos uma forma de pagamento.");

            string? chave = null, nome = null, cidade = null;
            if (requisicao.AceitaPix || !string.IsNullOrWhiteSpace(requisicao.ChavePix))
            {
                chave = PixCopiaECola.ValidarChave(requisicao.ChavePix)
                    ?? throw new ExcecaoNegocio("Chave PIX inválida. Use CPF, CNPJ, e-mail, telefone (+55...) ou chave aleatória.");
                nome = requisicao.NomeRecebedor?.Trim();
                cidade = requisicao.CidadeRecebedor?.Trim();
                if (string.IsNullOrEmpty(nome) || nome.Length > 25)
                    throw new ExcecaoNegocio("Informe o nome do recebedor do PIX (até 25 caracteres), como aparece no banco.");
                if (string.IsNullOrEmpty(cidade) || cidade.Length > 15)
                    throw new ExcecaoNegocio("Informe a cidade do recebedor do PIX (até 15 caracteres).");
            }

            var empresaId = ObterEmpresaId();
            var config = await db.ConfiguracoesPagamento.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
            if (config is null)
            {
                config = new ConfiguracaoPagamento { EmpresaId = empresaId };
                db.ConfiguracoesPagamento.Add(config);
            }

            var agora = relogio.GetUtcNow().UtcDateTime;
            config.Provedor = provedor;
            config.ChavePix = chave;
            config.NomeRecebedor = nome;
            config.CidadeRecebedor = cidade;
            config.AceitaDinheiro = requisicao.AceitaDinheiro;
            config.AceitaPix = requisicao.AceitaPix;
            config.AceitaCartao = requisicao.AceitaCartao;
            config.AtualizadoEm = agora;

            Auditoria.Registrar(db, await contextoPatio.ObterPatioIdAsync(ct), AcoesAuditoria.ConfiguracaoPagamentoAlterada, SituacaoAuditoria.Sucesso,
                null, agora,
                new()
                {
                    ["adminId"] = adminId.ToString(),
                    ["provedor"] = provedor,
                    ["formas"] = string.Join(",", FormasAceitas(config)),
                    ["chavePix"] = chave is null ? "" : Mascarar(chave)
                });

            await db.SaveChangesAsync(ct);
            return ParaDto(config);
        }

        // PIX de exemplo (R$ 1,00) para o admin testar a chave no app do banco antes de usar no pátio
        public async Task<string> GerarPixDeTesteAsync(CancellationToken ct = default)
        {
            var config = await ObterDaEmpresaAsync(db, ObterEmpresaId(), ct);
            if (config.ChavePix is null)
                throw new ExcecaoConflito("Cadastre e salve a chave PIX antes de testar.");
            return PixCopiaECola.Gerar(config.ChavePix, config.NomeRecebedor!, config.CidadeRecebedor!, 1.00m, "PKLOTSTESTE");
        }

        private static string Mascarar(string chave) => chave.Length <= 4 ? "****" : $"{new string('*', chave.Length - 4)}{chave[^4..]}";

        private static ConfiguracaoPagamentoDto ParaDto(ConfiguracaoPagamento c) =>
            new(c.Provedor, c.ChavePix, c.NomeRecebedor, c.CidadeRecebedor, c.AceitaDinheiro, c.AceitaPix, c.AceitaCartao, ProvedoresPagamento.Todos);

        private Guid ObterEmpresaId() =>
            contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Esta função é exclusiva de usuários de uma empresa.");
    }
}
