using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Fatura vencida: aviso durante a carência do plano; depois dela, bloqueio parcial até o pagamento.
    // O bloqueio barra novas entradas e mudanças na administração. Saídas, caixa e mensalidades seguem
    // funcionando, para não prender carro no pátio nem impedir o pátio de receber.
    public static class Inadimplencia
    {
        public const int CarenciaPadrao = 7;

        private static readonly SituacaoCobrancaDto EmDia = new(SituacaoAssinatura.EmDia, 0, 0, 0, null);

        public static async Task<SituacaoCobrancaDto> ObterAsync(IPkLotsDbContext db, Guid empresaId, DateOnly hoje, CancellationToken ct) =>
            (await ObterTodasAsync(db, hoje, ct, empresaId)).GetValueOrDefault(empresaId, EmDia);

        // Só as empresas com fatura vencida (as demais estão em dia)
        public static async Task<Dictionary<Guid, SituacaoCobrancaDto>> ObterTodasAsync(IPkLotsDbContext db, DateOnly hoje, CancellationToken ct,
            Guid? apenasEmpresa = null)
        {
            var vencidas = await db.Faturas.IgnoreQueryFilters()
                .Where(f => f.Situacao == SituacaoFatura.Aberta && f.Vencimento < hoje && (apenasEmpresa == null || f.EmpresaId == apenasEmpresa))
                .Select(f => new { f.EmpresaId, f.Vencimento, f.Valor })
                .ToListAsync(ct);
            if (vencidas.Count == 0)
                return [];

            var empresas = vencidas.Select(v => v.EmpresaId).Distinct().ToList();
            var carencias = await db.Assinaturas.IgnoreQueryFilters()
                .Where(a => empresas.Contains(a.EmpresaId))
                .ToDictionaryAsync(a => a.EmpresaId, a => a.Plano.DiasCarencia, ct);

            return vencidas.GroupBy(v => v.EmpresaId).ToDictionary(g => g.Key, g =>
            {
                var maisAntiga = g.Min(v => v.Vencimento);
                var atraso = hoje.DayNumber - maisAntiga.DayNumber;
                var carencia = carencias.GetValueOrDefault(g.Key, CarenciaPadrao);
                var bloqueada = atraso > carencia;
                return new SituacaoCobrancaDto(
                    bloqueada ? SituacaoAssinatura.Bloqueada : SituacaoAssinatura.Vencida,
                    atraso,
                    bloqueada ? 0 : carencia - atraso + 1,
                    g.Sum(v => v.Valor),
                    maisAntiga);
            });
        }

        public static async Task ExigirSemBloqueioAsync(IPkLotsDbContext db, Guid? empresaId, TimeProvider relogio, string acao, CancellationToken ct)
        {
            if (empresaId is null)
                return;
            var situacao = await ObterAsync(db, empresaId.Value, ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime), ct);
            if (situacao.Situacao == SituacaoAssinatura.Bloqueada)
                throw new ExcecaoProibido(
                    $"A fatura do PkLots está vencida há {situacao.DiasEmAtraso} dias. {acao} voltam a funcionar após o pagamento; " +
                    "saídas, caixa e mensalidades continuam liberados.");
        }
    }
}
