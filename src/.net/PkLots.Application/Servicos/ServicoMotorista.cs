using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Funcionalidades do app do motorista: veículos, estacionamento atual, histórico e saída
    public class ServicoMotorista(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ServicoVeiculos servicoVeiculos,
        ServicoSaida servicoSaida,
        TimeProvider relogio)
    {
        public async Task<IReadOnlyList<VeiculoDto>> ListarVeiculosAsync(Guid usuarioId, CancellationToken ct = default) =>
            await db.Veiculos
                .Where(v => v.UsuarioId == usuarioId)
                .OrderBy(v => v.Placa)
                .Select(v => new VeiculoDto(v.Id, v.Placa, v.Modelo, v.Cor))
                .ToListAsync(ct);

        public async Task<VeiculoDto> CadastrarVeiculoAsync(Guid usuarioId, CadastrarVeiculoRequisicao requisicao, CancellationToken ct = default)
        {
            var placa = ValidadorPlaca.Normalizar(requisicao.Placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            var veiculo = await db.Veiculos.FirstOrDefaultAsync(v => v.PatioId == patioId && v.Placa == placa, ct);
            if (veiculo is null)
            {
                veiculo = new Veiculo { PatioId = patioId, Placa = placa, CriadoEm = agora };
                db.Veiculos.Add(veiculo);
            }
            else if (veiculo.UsuarioId is not null && veiculo.UsuarioId != usuarioId)
            {
                throw new ExcecaoConflito($"O veículo {placa} já está vinculado a outro motorista.");
            }

            var usuario = await db.Usuarios.FirstAsync(u => u.Id == usuarioId, ct);
            veiculo.UsuarioId = usuarioId;
            veiculo.NomeProprietario = usuario.NomeCompleto;
            veiculo.Modelo = requisicao.Modelo ?? veiculo.Modelo;
            veiculo.Cor = requisicao.Cor ?? veiculo.Cor;
            veiculo.AtualizadoEm = agora;

            await db.SaveChangesAsync(ct);
            return new VeiculoDto(veiculo.Id, veiculo.Placa, veiculo.Modelo, veiculo.Cor);
        }

        public async Task<EstacionamentoAtualDto?> ObterEstacionamentoAtualAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var entrada = await ConsultarEntradaAtiva(usuarioId)
                .Select(e => new { e.Id, e.Placa, e.DataHoraEntrada, NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null, e.Patio.ValorHora })
                .FirstOrDefaultAsync(ct);

            if (entrada is null)
                return null;

            var agora = relogio.GetUtcNow().UtcDateTime;
            var tarifa = CalculadoraTarifa.Calcular(entrada.DataHoraEntrada, agora, entrada.ValorHora);

            return new EstacionamentoAtualDto(entrada.Id, entrada.Placa, entrada.DataHoraEntrada,
                tarifa.DuracaoFormatada, entrada.NumeroVaga, tarifa.ValorTotal);
        }

        // Últimos 30 dias de acessos dos veículos do motorista
        public async Task<IReadOnlyList<HistoricoVeiculoDto>> ObterHistoricoAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var placas = await db.Veiculos.Where(v => v.UsuarioId == usuarioId).Select(v => v.Placa).ToListAsync(ct);
            if (placas.Count == 0)
                return [];

            var desde = relogio.GetUtcNow().UtcDateTime.AddDays(-30);
            return await servicoVeiculos.ConsultarHistorico(patioId, placas, desde).ToListAsync(ct);
        }

        public async Task<SaidaDto> IniciarSaidaAsync(Guid usuarioId, CancellationToken ct = default)
        {
            var entradaId = await ConsultarEntradaAtiva(usuarioId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct)
                ?? throw new ExcecaoNaoEncontrado("Você não possui veículo estacionado no momento.");

            return await servicoSaida.RegistrarPorEntradaAsync(entradaId, ct);
        }

        private IQueryable<EntradaVeiculo> ConsultarEntradaAtiva(Guid usuarioId)
        {
            var placas = db.Veiculos.Where(v => v.UsuarioId == usuarioId).Select(v => v.Placa);
            return db.EntradasVeiculos
                .Where(e => e.Situacao == SituacaoEntrada.Ativa && placas.Contains(e.Placa))
                .OrderByDescending(e => e.DataHoraEntrada);
        }
    }
}
