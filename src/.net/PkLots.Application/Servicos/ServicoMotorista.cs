using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // App do motorista: as placas pertencem à conta e valem em qualquer pátio da plataforma
    public class ServicoMotorista(
        IPkLotsDbContext db,
        ServicoVeiculos servicoVeiculos,
        ServicoSaida servicoSaida,
        ServicoVagas servicoVagas,
        TimeProvider relogio)
    {
        public async Task<IReadOnlyList<VeiculoDto>> ListarVeiculosAsync(Guid usuarioId, CancellationToken ct = default) =>
            await db.VeiculosMotoristas
                .Where(v => v.UsuarioId == usuarioId)
                .OrderBy(v => v.Placa)
                .Select(v => new VeiculoDto(v.Id, v.Placa, v.Modelo, v.Cor))
                .ToListAsync(ct);

        public async Task<VeiculoDto> CadastrarVeiculoAsync(Guid usuarioId, CadastrarVeiculoRequisicao requisicao, CancellationToken ct = default)
        {
            var placa = ValidadorPlaca.Normalizar(requisicao.Placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");
            var agora = relogio.GetUtcNow().UtcDateTime;

            var veiculo = await db.VeiculosMotoristas.FirstOrDefaultAsync(v => v.Placa == placa, ct);
            if (veiculo is null)
            {
                veiculo = new VeiculoMotorista { UsuarioId = usuarioId, Placa = placa, CriadoEm = agora };
                db.VeiculosMotoristas.Add(veiculo);
            }
            else if (veiculo.UsuarioId != usuarioId)
            {
                throw new ExcecaoConflito($"O veículo {placa} já está vinculado a outro motorista.");
            }

            veiculo.Modelo = requisicao.Modelo ?? veiculo.Modelo;
            veiculo.Cor = requisicao.Cor ?? veiculo.Cor;
            veiculo.AtualizadoEm = agora;

            await db.SaveChangesAsync(ct);
            return new VeiculoDto(veiculo.Id, veiculo.Placa, veiculo.Modelo, veiculo.Cor);
        }

        public async Task<EstacionamentoAtualDto?> ObterEstacionamentoAtualAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var entrada = await ConsultarEntradaAtiva(usuarioId)
                .Select(e => new
                {
                    e.Id,
                    e.Placa,
                    e.DataHoraEntrada,
                    NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null,
                    e.Patio.ValorHora,
                    e.Patio.MinutosTolerancia,
                    NomePatio = e.Patio.Nome
                })
                .FirstOrDefaultAsync(ct);

            if (entrada is null)
                return null;

            var agora = relogio.GetUtcNow().UtcDateTime;
            var tarifa = CalculadoraTarifa.Calcular(entrada.DataHoraEntrada, agora, entrada.ValorHora, entrada.MinutosTolerancia);

            return new EstacionamentoAtualDto(entrada.Id, entrada.Placa, entrada.DataHoraEntrada,
                tarifa.DuracaoFormatada, entrada.NumeroVaga, tarifa.ValorTotal, entrada.NomePatio);
        }

        // Últimos 30 dias de acessos dos veículos do motorista, em todos os pátios
        public async Task<IReadOnlyList<HistoricoVeiculoDto>> ObterHistoricoAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var placas = await PlacasDoMotorista(usuarioId).ToListAsync(ct);
            if (placas.Count == 0)
                return [];

            var desde = relogio.GetUtcNow().UtcDateTime.AddDays(-30);
            return await servicoVeiculos.ConsultarHistorico(null, placas, desde).ToListAsync(ct);
        }

        public async Task<SaidaDto> IniciarSaidaAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var entradaId = await ConsultarEntradaAtiva(usuarioId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct)
                ?? throw new ExcecaoNaoEncontrado("Você não possui veículo estacionado no momento.");

            return await servicoSaida.RegistrarPorEntradaAsync(entradaId, ct);
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

        private IQueryable<string> PlacasDoMotorista(Guid usuarioId) =>
            db.VeiculosMotoristas.Where(v => v.UsuarioId == usuarioId).Select(v => v.Placa);

        private IQueryable<EntradaVeiculo> ConsultarEntradaAtiva(Guid usuarioId)
        {
            var placas = PlacasDoMotorista(usuarioId);
            return db.EntradasVeiculos
                .Where(e => e.Situacao == SituacaoEntrada.Ativa && placas.Contains(e.Placa))
                .OrderByDescending(e => e.DataHoraEntrada);
        }
    }
}
