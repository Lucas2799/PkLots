using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Caixa por operador: abre com troco, recebe (saídas e mensalidades), sangria/suprimento, fecha conferindo o dinheiro
    public class ServicoCaixa(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        public const decimal ValorMaximo = 100_000m;

        // Caixa aberto do usuário no pátio, exigido para receber pagamentos
        public static async Task<Caixa> ExigirCaixaAbertoAsync(IPkLotsDbContext db, Guid usuarioId, Guid patioId, CancellationToken ct)
        {
            var caixa = await db.Caixas.Include(c => c.Patio).FirstOrDefaultAsync(c => c.OperadorId == usuarioId && c.FechadoEm == null, ct)
                ?? throw new ExcecaoConflito("Abra o seu caixa para receber pagamentos.");
            if (caixa.PatioId != patioId)
                throw new ExcecaoConflito($"Seu caixa está aberto no pátio {caixa.Patio.Nome}. Feche-o ou troque para esse pátio.");
            return caixa;
        }

        public async Task<CaixaDto?> ObterAtualAsync(CancellationToken ct = default)
        {
            var usuarioId = ObterUsuarioId();
            var caixa = await db.Caixas.FirstOrDefaultAsync(c => c.OperadorId == usuarioId && c.FechadoEm == null, ct);
            return caixa is null ? null : await ResumirAsync(caixa, ct);
        }

        public async Task<CaixaDto> AbrirAsync(AbrirCaixaRequisicao requisicao, CancellationToken ct = default)
        {
            ValidarValor(requisicao.TrocoInicial, permitirZero: true, "troco inicial");
            var usuarioId = ObterUsuarioId();
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            if (await db.Caixas.AnyAsync(c => c.OperadorId == usuarioId && c.FechadoEm == null, ct))
                throw new ExcecaoConflito("Você já tem um caixa aberto. Feche-o antes de abrir outro.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            var caixa = new Caixa { PatioId = patioId, OperadorId = usuarioId, AbertoEm = agora, TrocoInicial = requisicao.TrocoInicial };
            db.Caixas.Add(caixa);
            Auditoria.Registrar(db, patioId, AcoesAuditoria.CaixaAberto, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["usuarioId"] = usuarioId.ToString(), ["caixaId"] = caixa.Id.ToString(), ["troco"] = requisicao.TrocoInicial });

            await db.SaveChangesAsync(ct);
            return await ResumirAsync(caixa, ct);
        }

        public async Task<CaixaDto> MovimentarAsync(MovimentarCaixaRequisicao requisicao, CancellationToken ct = default)
        {
            var tipo = (requisicao.Tipo ?? "").Trim().ToLowerInvariant();
            if (tipo is not (TipoMovimentoCaixa.Sangria or TipoMovimentoCaixa.Suprimento))
                throw new ExcecaoNegocio("Tipo inválido: use sangria ou suprimento.");
            ValidarValor(requisicao.Valor, permitirZero: false, "valor");

            var caixa = await ObterMeuCaixaAbertoAsync(ct);
            if (tipo == TipoMovimentoCaixa.Sangria)
            {
                var disponivel = (await ResumirAsync(caixa, ct)).DinheiroEsperado;
                if (requisicao.Valor > disponivel)
                    throw new ExcecaoConflito($"A sangria ({requisicao.Valor:0.00}) é maior que o dinheiro em caixa ({disponivel:0.00}).");
            }

            var agora = relogio.GetUtcNow().UtcDateTime;
            db.MovimentosCaixa.Add(new MovimentoCaixa
            {
                CaixaId = caixa.Id,
                Tipo = tipo,
                Valor = requisicao.Valor,
                Motivo = string.IsNullOrWhiteSpace(requisicao.Motivo) ? null : requisicao.Motivo.Trim(),
                CriadoEm = agora
            });
            Auditoria.Registrar(db, caixa.PatioId, AcoesAuditoria.MovimentoCaixa, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["caixaId"] = caixa.Id.ToString(), ["tipo"] = tipo, ["valor"] = requisicao.Valor });

            await db.SaveChangesAsync(ct);
            return await ResumirAsync(caixa, ct);
        }

        public async Task<CaixaDto> FecharAsync(FecharCaixaRequisicao requisicao, CancellationToken ct = default)
        {
            ValidarValor(requisicao.DinheiroContado, permitirZero: true, "dinheiro contado");
            var caixa = await ObterMeuCaixaAbertoAsync(ct);
            var resumo = await ResumirAsync(caixa, ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            caixa.FechadoEm = agora;
            caixa.DinheiroEsperado = resumo.DinheiroEsperado;
            caixa.DinheiroContado = requisicao.DinheiroContado;
            caixa.ObservacaoFechamento = string.IsNullOrWhiteSpace(requisicao.Observacao) ? null : requisicao.Observacao.Trim();

            var diferenca = requisicao.DinheiroContado - resumo.DinheiroEsperado;
            Auditoria.Registrar(db, caixa.PatioId, AcoesAuditoria.CaixaFechado,
                diferenca == 0 ? SituacaoAuditoria.Sucesso : SituacaoAuditoria.Alerta, null, agora,
                new()
                {
                    ["caixaId"] = caixa.Id.ToString(),
                    ["esperado"] = resumo.DinheiroEsperado,
                    ["contado"] = requisicao.DinheiroContado,
                    ["diferenca"] = diferenca,
                    ["totalRecebido"] = resumo.TotalRecebido
                });

            await db.SaveChangesAsync(ct);
            return await ResumirAsync(caixa, ct);
        }

        // Caixas do pátio num dia (admin vê todos; operador só os próprios): relatório do dia
        public async Task<IReadOnlyList<CaixaDto>> ListarAsync(DateOnly? data, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var dia = data ?? ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime);
            var inicio = TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil);
            var fim = inicio.AddDays(1);

            var consulta = db.Caixas.Where(c => c.PatioId == patioId && c.AbertoEm < fim && (c.FechadoEm == null || c.FechadoEm >= inicio));
            if (contextoPatio.Perfil != Perfis.Admin)
            {
                var usuarioId = ObterUsuarioId();
                consulta = consulta.Where(c => c.OperadorId == usuarioId);
            }

            var resultado = new List<CaixaDto>();
            foreach (var caixa in await consulta.OrderBy(c => c.AbertoEm).ToListAsync(ct))
                resultado.Add(await ResumirAsync(caixa, ct));
            return resultado;
        }

        public async Task<CaixaDto> ResumirAsync(Caixa caixa, CancellationToken ct = default)
        {
            var saidas = await db.SaidasVeiculos
                .Where(s => s.CaixaId == caixa.Id && s.FormaPagamento != null)
                .Select(s => new { Forma = s.FormaPagamento!, Valor = s.ValorTotal })
                .ToListAsync(ct);
            var mensalidades = await db.PagamentosMensalidade
                .Where(p => p.CaixaId == caixa.Id)
                .Select(p => new { Forma = p.FormaPagamento, p.Valor })
                .ToListAsync(ct);
            var movimentos = await db.MovimentosCaixa.Where(m => m.CaixaId == caixa.Id).OrderBy(m => m.CriadoEm).ToListAsync(ct);
            var nomes = await db.Caixas.Where(c => c.Id == caixa.Id)
                .Select(c => new { Patio = c.Patio.Nome, Operador = c.Operador.NomeCompleto })
                .FirstOrDefaultAsync(ct);

            var recebimentos = saidas.Concat(mensalidades)
                .GroupBy(r => r.Forma)
                .Select(g => new RecebimentoPorFormaDto(g.Key, g.Count(), g.Sum(r => r.Valor)))
                .OrderBy(r => r.Forma)
                .ToList();

            var dinheiro = recebimentos.Where(r => r.Forma == FormasPagamento.Dinheiro).Sum(r => r.Total);
            var suprimentos = movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Suprimento).Sum(m => m.Valor);
            var sangrias = movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Sangria).Sum(m => m.Valor);
            var esperado = caixa.DinheiroEsperado ?? caixa.TrocoInicial + dinheiro + suprimentos - sangrias;

            return new CaixaDto(
                caixa.Id,
                caixa.PatioId,
                nomes?.Patio ?? "",
                caixa.OperadorId,
                nomes?.Operador ?? "",
                caixa.AbertoEm,
                caixa.FechadoEm,
                caixa.TrocoInicial,
                recebimentos,
                saidas.Count,
                recebimentos.Sum(r => r.Total),
                suprimentos,
                sangrias,
                esperado,
                caixa.DinheiroContado,
                caixa.DinheiroContado is { } contado ? contado - esperado : null,
                caixa.ObservacaoFechamento,
                movimentos.Select(m => new MovimentoCaixaDto(m.Id, m.Tipo, m.Valor, m.Motivo, m.CriadoEm)).ToList());
        }

        private async Task<Caixa> ObterMeuCaixaAbertoAsync(CancellationToken ct)
        {
            var usuarioId = ObterUsuarioId();
            return await db.Caixas.Include(c => c.Movimentos).FirstOrDefaultAsync(c => c.OperadorId == usuarioId && c.FechadoEm == null, ct)
                ?? throw new ExcecaoConflito("Você não tem caixa aberto.");
        }

        private static void ValidarValor(decimal valor, bool permitirZero, string campo)
        {
            if (valor < 0 || (!permitirZero && valor == 0) || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
                throw new ExcecaoNegocio($"Valor inválido para {campo}.");
        }

        private Guid ObterUsuarioId() =>
            contextoPatio.UsuarioId ?? throw new ExcecaoNaoAutorizado("Usuário não identificado.");
    }
}
