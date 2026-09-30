using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    public class ServicoVagas(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ICacheVagas cache,
        TimeProvider relogio)
    {
        public async Task<IReadOnlyList<VagaDto>> ListarAsync(CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            return await db.Vagas
                .Where(v => v.PatioId == patioId && v.Ativa)
                .OrderBy(v => v.Fileira).ThenBy(v => v.Indice)
                .Select(v => new VagaDto(
                    v.Id,
                    v.Numero,
                    v.Fileira,
                    v.Indice,
                    v.Situacao,
                    db.Veiculos.Where(ve => ve.Id == v.VeiculoAtualId).Select(ve => ve.Placa).FirstOrDefault(),
                    v.OcupadaDesde,
                    v.Observacoes))
                .ToListAsync(ct);
        }

        // Resumo servido do Redis (TTL 5 min); recalculado do banco quando não há cache
        public async Task<ResumoVagasDto> ObterResumoAsync(CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            var emCache = await cache.ObterAsync(patioId);
            if (emCache is not null)
                return emCache;

            var contagem = await db.Vagas
                .Where(v => v.PatioId == patioId && v.Ativa)
                .GroupBy(v => v.Situacao)
                .Select(g => new { Situacao = g.Key, Quantidade = g.Count() })
                .ToListAsync(ct);

            int Quantidade(string situacao) => contagem.FirstOrDefault(c => c.Situacao == situacao)?.Quantidade ?? 0;

            var resumo = new ResumoVagasDto(
                contagem.Sum(c => c.Quantidade),
                Quantidade(SituacaoVaga.Disponivel),
                Quantidade(SituacaoVaga.Ocupada),
                Quantidade(SituacaoVaga.Manutencao),
                relogio.GetUtcNow().UtcDateTime);

            await cache.DefinirAsync(patioId, resumo);
            return resumo;
        }

        public async Task<VagaDto> MarcarManutencaoAsync(Guid vagaId, string? observacoes, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var vaga = await db.Vagas.FirstOrDefaultAsync(v => v.Id == vagaId && v.PatioId == patioId && v.Ativa, ct)
                ?? throw new ExcecaoNaoEncontrado("Vaga não encontrada.");

            if (vaga.Ocupada)
                throw new ExcecaoConflito($"A vaga {vaga.Numero} está ocupada e não pode entrar em manutenção.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            vaga.Disponivel = false;
            vaga.Situacao = SituacaoVaga.Manutencao;
            vaga.Observacoes = observacoes;
            vaga.AtualizadoEm = agora;

            Auditoria.Registrar(db, patioId, AcoesAuditoria.VagaManutencao, SituacaoAuditoria.Alerta, null, agora,
                new() { ["vaga"] = vaga.Numero, ["observacoes"] = observacoes ?? "" });

            await db.SaveChangesAsync(ct);
            await cache.InvalidarAsync(patioId);

            return new VagaDto(vaga.Id, vaga.Numero, vaga.Fileira, vaga.Indice, vaga.Situacao, null, null, vaga.Observacoes);
        }

        public async Task<VagaDto> LiberarManutencaoAsync(Guid vagaId, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var vaga = await db.Vagas.FirstOrDefaultAsync(v => v.Id == vagaId && v.PatioId == patioId && v.Ativa, ct)
                ?? throw new ExcecaoNaoEncontrado("Vaga não encontrada.");

            if (vaga.Situacao != SituacaoVaga.Manutencao)
                throw new ExcecaoConflito($"A vaga {vaga.Numero} não está em manutenção.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            vaga.Disponivel = true;
            vaga.Situacao = SituacaoVaga.Disponivel;
            vaga.Observacoes = null;
            vaga.AtualizadoEm = agora;

            Auditoria.Registrar(db, patioId, AcoesAuditoria.VagaLiberada, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["vaga"] = vaga.Numero });

            await db.SaveChangesAsync(ct);
            await cache.InvalidarAsync(patioId);

            return new VagaDto(vaga.Id, vaga.Numero, vaga.Fileira, vaga.Indice, vaga.Situacao, null, null, null);
        }
    }
}
