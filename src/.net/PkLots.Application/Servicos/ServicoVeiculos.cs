using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;

namespace PkLots.Application.Servicos
{
    public class ServicoVeiculos(IPkLotsDbContext db, IContextoPatio contextoPatio)
    {
        public async Task<IReadOnlyList<HistoricoVeiculoDto>> ObterHistoricoAsync(string placa, DateTime? desde = null, CancellationToken ct = default)
        {
            var placaNormalizada = ValidadorPlaca.Normalizar(placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            return await ConsultarHistorico(patioId, [placaNormalizada], desde).ToListAsync(ct);
        }

        // patioId nulo: todos os pátios (histórico do motorista)
        internal IQueryable<HistoricoVeiculoDto> ConsultarHistorico(Guid? patioId, IReadOnlyCollection<string> placas, DateTime? desde)
        {
            var consulta = db.EntradasVeiculos.Where(e => placas.Contains(e.Placa));
            if (patioId is not null)
                consulta = consulta.Where(e => e.PatioId == patioId);
            if (desde is not null)
                consulta = consulta.Where(e => e.DataHoraEntrada >= desde);

            return consulta
                .OrderByDescending(e => e.DataHoraEntrada)
                .Select(e => new
                {
                    Entrada = e,
                    NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null,
                    NomePatio = e.Patio.Nome,
                    Saida = db.SaidasVeiculos.FirstOrDefault(s => s.EntradaVeiculoId == e.Id)
                })
                .Select(x => new HistoricoVeiculoDto(
                    x.Entrada.Id,
                    x.Entrada.Placa,
                    x.Entrada.DataHoraEntrada,
                    x.Saida != null ? x.Saida.DataHoraSaida : null,
                    x.NumeroVaga,
                    x.Saida != null ? x.Saida.DuracaoFormatada : null,
                    x.Saida != null ? x.Saida.ValorTotal : null,
                    x.Entrada.Situacao,
                    x.NomePatio));
        }
    }
}
