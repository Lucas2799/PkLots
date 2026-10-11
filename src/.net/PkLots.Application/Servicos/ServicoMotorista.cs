using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // App do mensalista (perfil "motorista"). A conta só enxerga os contratos em que aceitou o convite do pátio,
    // e de cada contrato só as placas dele, naquele pátio, a partir do vínculo. Fora disso a pessoa é avulsa:
    // acompanha a estadia pelo link do ticket, como qualquer cliente.
    public class ServicoMotorista(
        IPkLotsDbContext db,
        ServicoVeiculos servicoVeiculos,
        ServicoVagas servicoVagas,
        TimeProvider relogio)
    {
        public const int DiasHistorico = 30;

        public async Task<IReadOnlyList<ContratoMensalistaDto>> ListarContratosAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var contratos = await ContratosAtivos(usuarioId)
                .Include(m => m.Placas)
                .Include(m => m.Patio).ThenInclude(p => p.Empresa)
                .OrderBy(m => m.Patio.Nome)
                .ToListAsync(ct);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var hoje = ServicoMensalistas.Hoje(agora);
            var resultado = new List<ContratoMensalistaDto>(contratos.Count);
            foreach (var m in contratos)
            {
                var placas = m.Placas.Select(p => p.Placa).OrderBy(p => p).ToList();
                var emDia = ServicoMensalistas.EmDia(m, hoje);
                var estacionados = await db.EntradasVeiculos
                    .Where(e => e.PatioId == m.PatioId && e.Situacao == SituacaoEntrada.Ativa && placas.Contains(e.Placa))
                    .Select(e => new { e.Placa, e.DataHoraEntrada, e.MensalistaId, NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null })
                    .ToListAsync(ct);

                resultado.Add(new ContratoMensalistaDto(
                    m.Id, m.Patio.Nome, m.Patio.Empresa.Nome, placas, m.ValidoAte,
                    ServicoMensalistas.Situacao(m, hoje),
                    m.ValidoAte is { } ate ? ate.DayNumber - hoje.DayNumber : null,
                    estacionados.Select(e =>
                    {
                        var tarifa = CalculadoraTarifa.Calcular(e.DataHoraEntrada, agora, m.Patio.ValorHora, m.Patio.MinutosTolerancia);
                        // Mesma regra da saída: entrou em dia ou está em dia agora → sem cobrança
                        var coberto = e.MensalistaId is not null || emDia;
                        return new EstadiaAtualDto(e.Placa, e.DataHoraEntrada, tarifa.DuracaoFormatada, e.NumeroVaga,
                            coberto ? 0m : tarifa.ValorTotal);
                    }).ToList()));
            }
            return resultado;
        }

        // Últimos 30 dias das placas de cada contrato, no pátio do contrato, nunca antes do vínculo
        public async Task<IReadOnlyList<HistoricoVeiculoDto>> ObterHistoricoAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var contratos = await ContratosAtivos(usuarioId)
                .Select(m => new { m.PatioId, m.VinculadoEm, Placas = m.Placas.Select(p => p.Placa).ToList() })
                .ToListAsync(ct);

            var limite = relogio.GetUtcNow().UtcDateTime.AddDays(-DiasHistorico);
            var historico = new List<HistoricoVeiculoDto>();
            foreach (var contrato in contratos)
            {
                var desde = contrato.VinculadoEm is { } vinculo && vinculo > limite ? vinculo : limite;
                historico.AddRange(await servicoVeiculos.ConsultarHistorico(contrato.PatioId, contrato.Placas, desde).ToListAsync(ct));
            }
            return historico.OrderByDescending(h => h.DataHoraEntrada).ToList();
        }

        // Pátios ativos (de empresas ativas) com as vagas disponíveis agora
        public async Task<IReadOnlyList<PatioDisponibilidadeDto>> ListarPatiosAsync(CancellationToken ct = default)
        {
            var patios = await db.Patios
                .Where(p => p.Ativo && p.Empresa.Ativa)
                .OrderBy(p => p.Empresa.Nome).ThenBy(p => p.Nome)
                .Select(p => new { p.Id, p.Nome, NomeEmpresa = p.Empresa.Nome })
                .ToListAsync(ct);

            var resultado = new List<PatioDisponibilidadeDto>(patios.Count);
            foreach (var patio in patios)
            {
                var resumo = await servicoVagas.ObterResumoDoPatioAsync(patio.Id, ct);
                resultado.Add(new PatioDisponibilidadeDto(patio.Id, patio.Nome, patio.NomeEmpresa, resumo.DisponiveisAvulsos, resumo.Total));
            }
            return resultado;
        }

        // Contrato ativo, vinculado a esta conta, em pátio e empresa ativos
        private IQueryable<Mensalista> ContratosAtivos(Guid usuarioId) =>
            db.Mensalistas.Where(m => m.UsuarioId == usuarioId && m.Ativo && m.VinculadoEm != null
                && m.Patio.Ativo && m.Patio.Empresa.Ativa);
    }
}
