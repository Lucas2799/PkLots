using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Estadia vista pelo link do QR do ticket. Quem tem o ticket vê aquela estadia (como no balcão:
    // quem tem o ticket retira o carro), e nada mais: nenhuma consulta parte da placa.
    public class ServicoEstadiaPublica(IPkLotsDbContext db, TimeProvider relogio)
    {
        // Depois da saída (ou do cancelamento) o link ainda mostra o recibo por este tempo
        public static readonly TimeSpan ValidadeAposSaida = TimeSpan.FromHours(24);

        private const string NaoEncontrado = "Ticket não encontrado ou link expirado.";

        public async Task<EstadiaPublicaDto> ObterAsync(string token, CancellationToken ct = default)
        {
            if (!TokenSeguro.FormatoValido(token, TokenSeguro.BytesTicket))
                throw new ExcecaoNaoEncontrado(NaoEncontrado);

            var dados = await db.EntradasVeiculos
                .Where(e => e.TokenAcesso == token && e.Patio.Ativo && e.Patio.Empresa.Ativa)
                .Select(e => new
                {
                    Entrada = e,
                    NumeroVaga = e.Vaga != null ? e.Vaga.Numero : null,
                    e.Patio.Nome,
                    e.Patio.ValorHora,
                    e.Patio.TabelaPrecoJson,
                    e.Patio.MinutosTolerancia,
                    Saida = db.SaidasVeiculos
                        .Where(s => s.EntradaVeiculoId == e.Id)
                        .Select(s => new { s.DataHoraSaida, s.ValorTotal, s.DuracaoFormatada, s.FormaPagamento, s.SituacaoPagamento })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new ExcecaoNaoEncontrado(NaoEncontrado);

            var entrada = dados.Entrada;
            var agora = relogio.GetUtcNow().UtcDateTime;
            var encerradaEm = dados.Saida?.DataHoraSaida ?? entrada.CanceladaEm;
            if (encerradaEm is { } fim && agora - fim > ValidadeAposSaida)
                throw new ExcecaoNaoEncontrado(NaoEncontrado);

            var tabela = TabelaPreco.De(dados.TabelaPrecoJson, dados.ValorHora);
            var codigo = CodigoTicket.Formatar(entrada.CodigoTicket ?? ServicoImpressao.Codigo(entrada.Id));

            if (entrada.Situacao == SituacaoEntrada.Ativa)
            {
                // Mesma regra da cobrança na saída: mensalista que entrou em dia (ou está em dia agora) não paga
                var tarifa = CalculadoraTarifa.Calcular(entrada.DataHoraEntrada, agora, tabela, dados.MinutosTolerancia);
                var mensalista = await ServicoMensalistas.BuscarPorPlacaAsync(db, entrada.PatioId, entrada.Placa, ct);
                var coberto = entrada.MensalistaId is not null
                    || (mensalista is not null && ServicoMensalistas.EmDia(mensalista, ServicoMensalistas.Hoje(agora)));

                return new EstadiaPublicaDto(dados.Nome, entrada.Placa, dados.NumeroVaga, codigo, entrada.DataHoraEntrada,
                    SituacaoEntrada.Ativa, tarifa.DuracaoFormatada, coberto ? 0m : tarifa.ValorTotal, dados.ValorHora,
                    dados.MinutosTolerancia, coberto, null, null, agora, tabela.Descricao());
            }

            if (dados.Saida is { } saida)
                return new EstadiaPublicaDto(dados.Nome, entrada.Placa, dados.NumeroVaga, codigo, entrada.DataHoraEntrada,
                    SituacaoEntrada.Concluida, saida.DuracaoFormatada, saida.ValorTotal, dados.ValorHora, dados.MinutosTolerancia,
                    saida.SituacaoPagamento == SituacaoPagamento.Mensalista, saida.DataHoraSaida, saida.FormaPagamento, agora, tabela.Descricao());

            // Cancelada: não mostra o motivo (é anotação interna do pátio)
            var duracao = CalculadoraTarifa.FormatarDuracao((entrada.CanceladaEm ?? agora) - entrada.DataHoraEntrada);
            return new EstadiaPublicaDto(dados.Nome, entrada.Placa, dados.NumeroVaga, codigo, entrada.DataHoraEntrada,
                SituacaoEntrada.Cancelada, duracao, 0m, dados.ValorHora, dados.MinutosTolerancia, false,
                entrada.CanceladaEm, null, agora, tabela.Descricao());
        }
    }
}
