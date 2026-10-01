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

            // Mensalista em dia usa qualquer vaga livre (inclusive as reservadas);
            // avulso (ou mensalista vencido) só entra se sobrar vaga além das reservadas ainda não usadas
            var mensalista = await ServicoMensalistas.BuscarPorPlacaAsync(db, patioId, placa, ct);
            var hoje = ServicoMensalistas.Hoje(agora);
            var mensalistaEmDia = mensalista is not null && ServicoMensalistas.EmDia(mensalista, hoje);

            var vagasLivres = await db.Vagas.CountAsync(v => v.PatioId == patioId && v.Ativa && v.Situacao == SituacaoVaga.Disponivel, ct);
            if (vagasLivres == 0)
            {
                await RecusarAsync(patioId, placa, agora, "patio_lotado", ct);
                throw new ExcecaoConflito("Pátio lotado: não há vagas disponíveis.");
            }
            if (!mensalistaEmDia)
            {
                var reservadas = await db.Patios.Where(p => p.Id == patioId).Select(p => p.VagasReservadasMensalistas).FirstAsync(ct);
                var reservadasLivres = Math.Max(0, reservadas - await ServicoMensalistas.ContarMensalistasEstacionadosAsync(db, patioId, ct));
                if (vagasLivres <= reservadasLivres)
                {
                    await RecusarAsync(patioId, placa, agora, "lotado_para_avulsos", ct);
                    throw new ExcecaoConflito(mensalista is null
                        ? "Pátio lotado para avulsos: as vagas restantes são reservadas a mensalistas."
                        : $"Mensalidade de {mensalista.Nome} vencida e as vagas restantes são reservadas a mensalistas em dia.");
                }
            }

            var vaga = await db.Vagas
                .Where(v => v.PatioId == patioId && v.Ativa && v.Situacao == SituacaoVaga.Disponivel)
                .OrderBy(v => v.Fileira).ThenBy(v => v.Indice)
                .FirstAsync(ct);

            var veiculo = await db.Veiculos.FirstOrDefaultAsync(v => v.PatioId == patioId && v.Placa == placa, ct);
            if (veiculo is null)
            {
                veiculo = new Veiculo { PatioId = patioId, Placa = placa, CriadoEm = agora, AtualizadoEm = agora };
                db.Veiculos.Add(veiculo);
            }

            var entrada = new EntradaVeiculo
            {
                PatioId = patioId,
                CodigoTicket = await GerarCodigoTicketAsync(patioId, ct),
                MensalistaId = mensalistaEmDia ? mensalista!.Id : null,
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
                entrada.Confianca, vaga.Id, vaga.Numero, entrada.Situacao,
                Mensalista: mensalista?.Nome,
                SituacaoMensalista: mensalista is null ? null : ServicoMensalistas.Situacao(mensalista, hoje),
                MensalidadeValidaAte: mensalista?.ValidoAte);
        }

        private async Task<string> GerarCodigoTicketAsync(Guid patioId, CancellationToken ct)
        {
            // ~1 trilhão de combinações: colisão é improvável, mas confere mesmo assim
            for (var tentativa = 0; tentativa < 5; tentativa++)
            {
                var codigo = CodigoTicket.Gerar();
                if (!await db.EntradasVeiculos.AnyAsync(e => e.PatioId == patioId && e.CodigoTicket == codigo, ct))
                    return codigo;
            }
            throw new InvalidOperationException("Não foi possível gerar um código de ticket único.");
        }

        private async Task RecusarAsync(Guid patioId, string placa, DateTime agora, string motivo, CancellationToken ct)
        {
            Auditoria.Registrar(db, patioId, AcoesAuditoria.EntradaRecusada, SituacaoAuditoria.Alerta, placa, agora,
                new() { ["motivo"] = motivo });
            await db.SaveChangesAsync(ct);
        }
    }
}
