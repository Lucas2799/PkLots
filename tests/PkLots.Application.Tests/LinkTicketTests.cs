using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class LinkTicketTests
    {
        private static async Task<string> EntrarAsync(Cenario cenario, string placa = "ABC1234")
        {
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao(placa));
            return (await cenario.Db.EntradasVeiculos.SingleAsync(e => e.Placa == placa)).TokenAcesso!;
        }

        [Fact]
        public async Task Entrada_gera_token_e_o_qr_do_ticket_e_o_link()
        {
            using var cenario = new Cenario();
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            var token = (await cenario.Db.EntradasVeiculos.SingleAsync()).TokenAcesso;

            Assert.True(TokenSeguro.FormatoValido(token, TokenSeguro.BytesTicket));
            var ticket = await cenario.Impressao.ObterTicketEntradaAsync(entrada.Id);
            Assert.Equal($"https://pklots.teste/t/{token}", ticket.ConteudoQrCode);
        }

        [Fact]
        public async Task Pagina_mostra_tempo_e_valor_parcial_da_estadia()
        {
            using var cenario = new Cenario();
            var token = await EntrarAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromMinutes(90));

            var estadia = await cenario.EstadiaPublica.ObterAsync(token);

            Assert.Equal(SituacaoEntrada.Ativa, estadia.Situacao);
            Assert.Equal("ABC1234", estadia.Placa);
            Assert.Equal("A1", estadia.NumeroVaga);
            Assert.Equal("1h 30m", estadia.Duracao);
            Assert.Equal(22.50m, estadia.Valor);
            Assert.False(estadia.CobertoPorMensalidade);
        }

        [Fact]
        public async Task Mensalista_em_dia_aparece_sem_valor()
        {
            using var cenario = new Cenario();
            var mensalista = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId,
                new SalvarMensalistaRequisicao("Ana", null, null, null, ["ABC1234"]));
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, mensalista.Id,
                new RegistrarPagamentoRequisicao(1, 200m, FormasPagamento.Dinheiro, null));
            var token = await EntrarAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromHours(3));

            var estadia = await cenario.EstadiaPublica.ObterAsync(token);

            Assert.True(estadia.CobertoPorMensalidade);
            Assert.Equal(0m, estadia.Valor);
        }

        [Fact]
        public async Task Depois_da_saida_mostra_o_recibo_por_24h_e_depois_expira()
        {
            using var cenario = new Cenario();
            var token = await EntrarAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromHours(2));
            await cenario.SairAsync("ABC1234", FormasPagamento.Cartao);

            var recibo = await cenario.EstadiaPublica.ObterAsync(token);
            Assert.Equal(SituacaoEntrada.Concluida, recibo.Situacao);
            Assert.Equal(30.00m, recibo.Valor);
            Assert.Equal(FormasPagamento.Cartao, recibo.FormaPagamento);
            Assert.NotNull(recibo.DataHoraSaida);

            cenario.Relogio.Advance(ServicoEstadiaPublica.ValidadeAposSaida + TimeSpan.FromMinutes(1));
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.EstadiaPublica.ObterAsync(token));
        }

        [Fact]
        public async Task Entrada_cancelada_aparece_como_cancelada_sem_o_motivo()
        {
            using var cenario = new Cenario();
            var token = await EntrarAsync(cenario);
            var entrada = await cenario.Db.EntradasVeiculos.SingleAsync();
            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Placa digitada errada"));

            var estadia = await cenario.EstadiaPublica.ObterAsync(token);

            Assert.Equal(SituacaoEntrada.Cancelada, estadia.Situacao);
            Assert.Equal(0m, estadia.Valor);
        }

        [Theory]
        [InlineData("curto")]
        [InlineData("AAAAAAAAAAAAAAAAAAAAAA")] // formato válido, mas não existe
        [InlineData("../../api/admin/usuario")]
        public async Task Token_invalido_ou_inexistente_nao_encontra(string token)
        {
            using var cenario = new Cenario();
            await EntrarAsync(cenario);

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.EstadiaPublica.ObterAsync(token));
        }

        [Fact]
        public async Task Empresa_desativada_derruba_o_link()
        {
            using var cenario = new Cenario();
            var token = await EntrarAsync(cenario);
            cenario.Empresa.Ativa = false;
            await cenario.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.EstadiaPublica.ObterAsync(token));
        }

        [Fact]
        public async Task Saida_pelo_qr_com_link()
        {
            using var cenario = new Cenario();
            var token = await EntrarAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromHours(1));

            var saida = await cenario.SairPorTicketAsync($"https://pklots.teste/t/{token}");

            Assert.Equal("ABC1234", saida.Placa);
            Assert.Equal(15.00m, saida.ValorTotal);
        }

        [Fact]
        public async Task Saida_pelo_qr_antigo_e_pelo_codigo_digitado_continuam_funcionando()
        {
            using var cenario = new Cenario();
            await EntrarAsync(cenario, "ABC1234");
            await EntrarAsync(cenario, "XYZ9876");
            var codigos = await cenario.Db.EntradasVeiculos.ToDictionaryAsync(e => e.Placa, e => e.CodigoTicket!);

            Assert.Equal("ABC1234", (await cenario.SairPorTicketAsync(CodigoTicket.ConteudoQrCode(codigos["ABC1234"]))).Placa);
            Assert.Equal("XYZ9876", (await cenario.SairPorTicketAsync(CodigoTicket.Formatar(codigos["XYZ9876"]))).Placa);
        }

        [Fact]
        public async Task Link_de_ticket_de_outro_patio_nao_serve_na_saida()
        {
            using var cenario = new Cenario();
            var (_, outroPatio) = cenario.CriarOutraEmpresa();
            var alheia = new PkLots.Core.Entities.EntradaVeiculo
            {
                PatioId = outroPatio.Id, Placa = "XYZ9876", DataHoraEntrada = Cenario.Inicio.UtcDateTime,
                Situacao = SituacaoEntrada.Ativa, TokenAcesso = TokenSeguro.Gerar(TokenSeguro.BytesTicket)
            };
            cenario.Db.EntradasVeiculos.Add(alheia);
            await cenario.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.SairPorTicketAsync($"https://pklots.teste/t/{alheia.TokenAcesso}"));
        }

        [Fact]
        public async Task Entrada_antiga_ganha_token_na_reimpressao()
        {
            using var cenario = new Cenario();
            await EntrarAsync(cenario);
            var entrada = await cenario.Db.EntradasVeiculos.SingleAsync();
            entrada.TokenAcesso = null;
            await cenario.Db.SaveChangesAsync();

            var ticket = await cenario.Impressao.ObterTicketEntradaAsync(entrada.Id);

            Assert.NotNull(entrada.TokenAcesso);
            Assert.Equal($"https://pklots.teste/t/{entrada.TokenAcesso}", ticket.ConteudoQrCode);
            Assert.Equal(ticket.ConteudoQrCode, (await cenario.Impressao.ObterTicketEntradaAsync(entrada.Id)).ConteudoQrCode);
        }

        [Theory]
        [InlineData("https://pklots.com.br/t/AbCdEfGhIjKlMnOpQrStUv", "AbCdEfGhIjKlMnOpQrStUv")]
        [InlineData("http://192.168.0.10:8000/t/AbCdEfGhIjKlMnOpQrSt-_?x=1", "AbCdEfGhIjKlMnOpQrSt-_")]
        [InlineData("PKLOTS:K7M2Q9XD", null)]
        [InlineData("https://pklots.com.br/t/curto", null)]
        public void Extrai_token_do_link_lido(string lido, string? esperado) =>
            Assert.Equal(esperado, CodigoTicket.ExtrairToken(lido));
    }
}
