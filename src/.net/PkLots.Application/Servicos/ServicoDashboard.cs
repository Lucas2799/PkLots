using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    public class ServicoDashboard(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ServicoVagas servicoVagas,
        ServicoMensalistas servicoMensalistas,
        TimeProvider relogio)
    {
        public async Task<DashboardDto> ObterAsync(CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var agora = relogio.GetUtcNow().UtcDateTime;
            var inicioHoje = FusoHorario.InicioDoDiaUtc(agora);

            var patio = await db.Patios.FirstAsync(p => p.Id == patioId, ct);
            var vagas = await servicoVagas.ObterResumoAsync(ct);

            // Resumo de hoje
            var entradasHoje = await db.EntradasVeiculos
                .CountAsync(e => e.PatioId == patioId && e.DataHoraEntrada >= inicioHoje, ct);

            var saidasHoje = await db.SaidasVeiculos
                .Where(s => s.PatioId == patioId && s.DataHoraSaida >= inicioHoje)
                .Select(s => new { s.ValorTotal, s.DuracaoSegundos })
                .ToListAsync(ct);

            var mediaSegundos = saidasHoje.Count > 0 ? saidasHoje.Average(s => s.DuracaoSegundos) : 0;
            var hoje = new ResumoDiaDto(
                entradasHoje,
                saidasHoje.Count,
                saidasHoje.Sum(s => s.ValorTotal),
                CalculadoraTarifa.FormatarDuracao(TimeSpan.FromSeconds(mediaSegundos)));

            // Veículos estacionados agora
            var estacionados = await db.EntradasVeiculos
                .Where(e => e.PatioId == patioId && e.Situacao == SituacaoEntrada.Ativa)
                .OrderBy(e => e.DataHoraEntrada)
                .Select(e => new { e.Id, e.Placa, e.DataHoraEntrada, NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null })
                .ToListAsync(ct);

            var listaEstacionados = estacionados
                .Select(e => new VeiculoEstacionadoDto(e.Id, e.Placa, e.DataHoraEntrada,
                    CalculadoraTarifa.FormatarDuracao(agora - e.DataHoraEntrada), e.NumeroVaga))
                .ToList();

            var ocupacao = await CalcularOcupacao24hAsync(patioId, agora, vagas.Total, ct);

            var mensalistas = await servicoMensalistas.ObterResumoAsync(patioId, ct);
            return new DashboardDto(patio.Nome, hoje, vagas, ocupacao, listaEstacionados, mensalistas);
        }

        // Vagas ocupadas no início de cada uma das últimas 23 horas e no momento atual (último ponto)
        private async Task<IReadOnlyList<OcupacaoHoraDto>> CalcularOcupacao24hAsync(Guid patioId, DateTime agora, int totalVagas, CancellationToken ct)
        {
            var inicioJanela = agora.AddHours(-24);

            var periodos = await db.EntradasVeiculos
                .Where(e => e.PatioId == patioId && e.DataHoraEntrada <= agora)
                .Select(e => new
                {
                    e.DataHoraEntrada,
                    DataHoraSaida = db.SaidasVeiculos
                        .Where(s => s.EntradaVeiculoId == e.Id)
                        .Select(s => (DateTime?)s.DataHoraSaida)
                        .FirstOrDefault()
                })
                .Where(p => p.DataHoraSaida == null || p.DataHoraSaida >= inicioJanela)
                .ToListAsync(ct);

            var horaAtual = new DateTime(agora.Year, agora.Month, agora.Day, agora.Hour, 0, 0, DateTimeKind.Utc);

            return Enumerable.Range(0, 23)
                .Select(i => horaAtual.AddHours(i - 22))
                .Append(agora)
                .Select(hora =>
                {
                    var ocupadas = periodos.Count(p => p.DataHoraEntrada <= hora && (p.DataHoraSaida == null || p.DataHoraSaida > hora));
                    var percentual = totalVagas > 0 ? Math.Round(ocupadas * 100m / totalVagas, 1) : 0;
                    return new OcupacaoHoraDto(hora, ocupadas, percentual);
                })
                .ToList();
        }
    }
}
