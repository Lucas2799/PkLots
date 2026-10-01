using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    public class ServicoSaida(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ICacheVagas cache,
        ReconhecedorPlaca reconhecedor,
        TimeProvider relogio)
    {
        public async Task<SaidaDto> RegistrarPorImagemAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default)
        {
            var resultado = await reconhecedor.ReconhecerAsync(imagem, nomeArquivo, "saida", ct);
            return await RegistrarAsync(new RegistrarSaidaRequisicao(resultado.Placa!, resultado.Confianca), ct);
        }

        public async Task<SaidaDto> RegistrarAsync(RegistrarSaidaRequisicao requisicao, CancellationToken ct = default)
        {
            var placa = ValidadorPlaca.Normalizar(requisicao.Placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");

            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            var entrada = await db.EntradasVeiculos
                .Include(e => e.Vaga)
                .FirstOrDefaultAsync(e => e.PatioId == patioId && e.Placa == placa && e.Situacao == SituacaoEntrada.Ativa, ct);

            if (entrada is null)
            {
                // Placa lida na saída sem entrada correspondente: alerta para o operador
                Auditoria.Registrar(db, patioId, AcoesAuditoria.SaidaSemEntrada, SituacaoAuditoria.Alerta, placa,
                    relogio.GetUtcNow().UtcDateTime, new() { ["confianca"] = requisicao.Confianca });
                await db.SaveChangesAsync(ct);
                throw new ExcecaoNaoEncontrado($"Nenhuma entrada ativa encontrada para a placa {placa}.");
            }

            return await FinalizarAsync(entrada, requisicao.UrlFoto, requisicao.Confianca, ct);
        }

        // Saída pela leitura do ticket (leitor USB, câmera ou código digitado)
        public async Task<SaidaDto> RegistrarPorTicketAsync(string? lido, CancellationToken ct = default)
        {
            var codigo = CodigoTicket.Normalizar(lido)
                ?? throw new ExcecaoNegocio("Código de ticket inválido. Use o código de 8 caracteres impresso no ticket.");
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            var entrada = await db.EntradasVeiculos
                .Include(e => e.Vaga)
                .FirstOrDefaultAsync(e => e.PatioId == patioId && e.CodigoTicket == codigo, ct)
                ?? throw new ExcecaoNaoEncontrado($"Ticket {CodigoTicket.Formatar(codigo)} não encontrado.");

            if (entrada.Situacao != SituacaoEntrada.Ativa)
            {
                var saidaAnterior = await db.SaidasVeiculos
                    .Where(s => s.EntradaVeiculoId == entrada.Id)
                    .Select(s => (DateTime?)s.DataHoraSaida)
                    .FirstOrDefaultAsync(ct);
                var quando = saidaAnterior is null
                    ? ""
                    : $" em {TimeZoneInfo.ConvertTimeFromUtc(saidaAnterior.Value, FusoHorario.Brasil):dd/MM 'às' HH:mm}";
                throw new ExcecaoConflito($"Este ticket já foi usado: a saída de {entrada.Placa} foi registrada{quando}.");
            }

            return await FinalizarAsync(entrada, null, null, ct, origem: "ticket");
        }

        // Saída iniciada pelo próprio motorista no app
        public async Task<SaidaDto> RegistrarPorEntradaAsync(Guid entradaId, CancellationToken ct = default)
        {
            var entrada = await db.EntradasVeiculos
                .Include(e => e.Vaga)
                .FirstOrDefaultAsync(e => e.Id == entradaId && e.Situacao == SituacaoEntrada.Ativa, ct)
                ?? throw new ExcecaoNaoEncontrado("Entrada ativa não encontrada.");

            return await FinalizarAsync(entrada, null, null, ct, origem: "app_motorista");
        }

        private async Task<SaidaDto> FinalizarAsync(EntradaVeiculo entrada, string? urlFoto, decimal? confianca, CancellationToken ct, string origem = "placa")
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var patio = await db.Patios.FirstAsync(p => p.Id == entrada.PatioId, ct);
            var tarifa = CalculadoraTarifa.Calcular(entrada.DataHoraEntrada, agora, patio.ValorHora, patio.MinutosTolerancia);

            // Mensalista: sem cobrança se entrou em dia ou está em dia agora; vencido paga como avulso
            var mensalista = await ServicoMensalistas.BuscarPorPlacaAsync(db, entrada.PatioId, entrada.Placa, ct);
            var cobertoPorMensalidade = entrada.MensalistaId is not null
                || (mensalista is not null && ServicoMensalistas.EmDia(mensalista, ServicoMensalistas.Hoje(agora)));
            string? aviso = null;
            if (cobertoPorMensalidade)
                tarifa = tarifa with { ValorTotal = 0m };
            else if (mensalista is not null)
                aviso = mensalista.ValidoAte is { } venceu
                    ? $"Mensalidade de {mensalista.Nome} vencida em {venceu:dd/MM/yyyy}: cobrado como avulso."
                    : $"{mensalista.Nome} ainda não pagou a mensalidade: cobrado como avulso.";

            var saida = new SaidaVeiculo
            {
                PatioId = entrada.PatioId,
                EntradaVeiculoId = entrada.Id,
                Placa = entrada.Placa,
                DataHoraSaida = agora,
                UrlFotoSaida = urlFoto,
                DuracaoSegundos = tarifa.DuracaoSegundos,
                DuracaoFormatada = tarifa.DuracaoFormatada,
                ValorHora = patio.ValorHora,
                ValorTotal = tarifa.ValorTotal,
                Moeda = "BRL",
                SituacaoPagamento = cobertoPorMensalidade ? SituacaoPagamento.Mensalista
                    : tarifa.Isento ? SituacaoPagamento.Isento
                    : SituacaoPagamento.Calculado,
                Situacao = SituacaoEntrada.Concluida,
                CriadoEm = agora
            };
            db.SaidasVeiculos.Add(saida);

            entrada.Situacao = SituacaoEntrada.Concluida;

            var vaga = entrada.Vaga;
            if (vaga is not null)
            {
                vaga.Disponivel = true;
                vaga.Ocupada = false;
                vaga.Situacao = SituacaoVaga.Disponivel;
                vaga.VeiculoAtualId = null;
                vaga.OcupadaDesde = null;
                vaga.AtualizadoEm = agora;
            }

            var detalhes = new Dictionary<string, object>
            {
                ["vaga"] = vaga?.Numero ?? "",
                ["duracao"] = tarifa.DuracaoFormatada,
                ["valor"] = tarifa.ValorTotal,
                ["origem"] = origem
            };
            if (mensalista is not null)
                detalhes["mensalista"] = mensalista.Nome;
            if (aviso is not null)
                detalhes["aviso"] = aviso;
            if (confianca is not null)
                detalhes["confianca"] = confianca;

            Auditoria.Registrar(db, entrada.PatioId, AcoesAuditoria.SaidaSucesso, SituacaoAuditoria.Sucesso,
                entrada.Placa, agora, detalhes);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ExcecaoConflito("A saída deste veículo já está sendo processada. Tente novamente.");
            }

            await cache.InvalidarAsync(entrada.PatioId);

            return new SaidaDto(saida.Id, entrada.Id, entrada.Placa, entrada.DataHoraEntrada, agora, urlFoto,
                tarifa.DuracaoSegundos, tarifa.DuracaoFormatada, tarifa.DuracaoHoras, patio.ValorHora,
                tarifa.ValorTotal, saida.Moeda, saida.SituacaoPagamento, vaga?.Numero, patio.Nome,
                mensalista?.Nome, aviso);
        }
    }
}
