using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    public class ServicoEntrada(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ICacheVagas cache,
        ReconhecedorPlaca reconhecedor,
        TimeProvider relogio)
    {
        public async Task<EntradaDto> RegistrarPorImagemAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default)
        {
            var resultado = await reconhecedor.ReconhecerAsync(imagem, nomeArquivo, "entrada", ct);
            return await RegistrarAsync(
                new RegistrarEntradaRequisicao(resultado.Placa!, resultado.Confianca, resultado.Metodo), ct);
        }

        public async Task<EntradaDto> RegistrarAsync(RegistrarEntradaRequisicao requisicao, CancellationToken ct = default)
        {
            var placa = ValidadorPlaca.Normalizar(requisicao.Placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");

            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            var jaEstacionado = await db.EntradasVeiculos
                .AnyAsync(e => e.PatioId == patioId && e.Placa == placa && e.Situacao == SituacaoEntrada.Ativa, ct);
            if (jaEstacionado)
            {
                await RecusarAsync(patioId, placa, agora, "veiculo_ja_estacionado", ct);
                throw new ExcecaoConflito($"O veículo {placa} já está estacionado.");
            }

            var vaga = await db.Vagas
                .Where(v => v.PatioId == patioId && v.Situacao == SituacaoVaga.Disponivel)
                .OrderBy(v => v.Fileira).ThenBy(v => v.Indice)
                .FirstOrDefaultAsync(ct);
            if (vaga is null)
            {
                await RecusarAsync(patioId, placa, agora, "patio_lotado", ct);
                throw new ExcecaoConflito("Pátio lotado: não há vagas disponíveis.");
            }

            var veiculo = await db.Veiculos.FirstOrDefaultAsync(v => v.PatioId == patioId && v.Placa == placa, ct);
            if (veiculo is null)
            {
                veiculo = new Veiculo { PatioId = patioId, Placa = placa, CriadoEm = agora, AtualizadoEm = agora };
                db.Veiculos.Add(veiculo);
            }

            var entrada = new EntradaVeiculo
            {
                PatioId = patioId,
                VeiculoId = veiculo.Id,
                Placa = placa,
                VagaId = vaga.Id,
                DataHoraEntrada = agora,
                UrlFotoEntrada = requisicao.UrlFoto,
                MetodoReconhecimento = requisicao.MetodoReconhecimento,
                Confianca = requisicao.Confianca,
                Situacao = SituacaoEntrada.Ativa,
                CriadoEm = agora
            };
            db.EntradasVeiculos.Add(entrada);

            vaga.Disponivel = false;
            vaga.Ocupada = true;
            vaga.Situacao = SituacaoVaga.Ocupada;
            vaga.VeiculoAtualId = veiculo.Id;
            vaga.OcupadaDesde = agora;
            vaga.AtualizadoEm = agora;

            Auditoria.Registrar(db, patioId, AcoesAuditoria.EntradaSucesso, SituacaoAuditoria.Sucesso, placa, agora,
                new()
                {
                    ["vaga"] = vaga.Numero,
                    ["confianca"] = requisicao.Confianca,
                    ["metodo"] = requisicao.MetodoReconhecimento ?? ""
                });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ExcecaoConflito("A vaga foi ocupada por outro veículo ao mesmo tempo. Tente novamente.");
            }

            await cache.InvalidarAsync(patioId);

            return new EntradaDto(entrada.Id, placa, agora, entrada.UrlFotoEntrada, entrada.MetodoReconhecimento,
                entrada.Confianca, vaga.Id, vaga.Numero, entrada.Situacao);
        }

        private async Task RecusarAsync(Guid patioId, string placa, DateTime agora, string motivo, CancellationToken ct)
        {
            Auditoria.Registrar(db, patioId, AcoesAuditoria.EntradaRecusada, SituacaoAuditoria.Alerta, placa, agora,
                new() { ["motivo"] = motivo });
            await db.SaveChangesAsync(ct);
        }
    }
}
