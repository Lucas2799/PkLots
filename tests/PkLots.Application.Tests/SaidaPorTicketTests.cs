using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class CodigoTicketTests
    {
        [Fact]
        public void Gera_codigos_de_8_caracteres_sem_caracteres_ambiguos()
        {
            var codigos = Enumerable.Range(0, 2000).Select(_ => CodigoTicket.Gerar()).ToList();

            Assert.All(codigos, c =>
            {
                Assert.Equal(8, c.Length);
                Assert.All(c, ch => Assert.Contains(ch, CodigoTicket.Alfabeto));
                Assert.DoesNotContain('0', c);
                Assert.DoesNotContain('O', c);
                Assert.DoesNotContain('1', c);
                Assert.DoesNotContain('I', c);
            });
            Assert.True(codigos.Distinct().Count() > 1990); // aleatórios
        }

        [Theory]
        [InlineData("PKLOTS:K7M2Q9XD", "K7M2Q9XD")] // leitura do QR
        [InlineData("pklots:k7m2q9xd", "K7M2Q9XD")]
        [InlineData("K7M2-Q9XD", "K7M2Q9XD")]       // digitado como impresso
        [InlineData(" k7m2 q9xd ", "K7M2Q9XD")]
        [InlineData("6D11F4F9", "6D11F4F9")]        // código de ticket antigo
        public void Normaliza_o_que_vem_do_leitor_ou_digitado(string lido, string esperado) =>
            Assert.Equal(esperado, CodigoTicket.Normalizar(lido));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ABC1234")]   // placa, não ticket
        [InlineData("K7M2Q9XD9")]
        public void Rejeita_codigos_invalidos(string? lido) => Assert.Null(CodigoTicket.Normalizar(lido));

        [Fact]
        public void Formata_para_impressao() => Assert.Equal("K7M2-Q9XD", CodigoTicket.Formatar("K7M2Q9XD"));
    }

    public class SaidaPorTicketTests
    {
        [Fact]
        public async Task Entrada_recebe_codigo_de_ticket()
        {
            using var cenario = new Cenario();

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            Assert.NotNull(CodigoTicket.Normalizar((await cenario.Db.EntradasVeiculos.SingleAsync()).CodigoTicket));
        }

        [Fact]
        public async Task Leitura_do_QR_registra_a_saida_e_libera_a_vaga()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            var codigo = (await cenario.Db.EntradasVeiculos.SingleAsync()).CodigoTicket!;
            cenario.Relogio.Advance(TimeSpan.FromHours(2));

            var saida = await cenario.Saida.RegistrarPorTicketAsync($"PKLOTS:{codigo}");

            Assert.Equal("ABC1234", saida.Placa);
            Assert.Equal(30.00m, saida.ValorTotal);
            Assert.Equal(SituacaoVaga.Disponivel, (await cenario.Db.Vagas.SingleAsync(v => v.Numero == "A1")).Situacao);
            var log = await cenario.Db.LogsAuditoria.SingleAsync(l => l.Acao == AcoesAuditoria.SaidaSucesso);
            Assert.Equal("ticket", log.Detalhes["origem"].ToString());
        }

        [Fact]
        public async Task Codigo_digitado_com_hifen_tambem_funciona()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            var codigo = (await cenario.Db.EntradasVeiculos.SingleAsync()).CodigoTicket!;

            var saida = await cenario.Saida.RegistrarPorTicketAsync(CodigoTicket.Formatar(codigo).ToLowerInvariant());

            Assert.Equal("ABC1234", saida.Placa);
        }

        [Fact]
        public async Task Ticket_ja_usado_e_recusado_com_a_data_da_saida()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            var codigo = (await cenario.Db.EntradasVeiculos.SingleAsync()).CodigoTicket!;
            await cenario.Saida.RegistrarPorTicketAsync(codigo);

            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Saida.RegistrarPorTicketAsync(codigo));

            Assert.Contains("já foi usado", erro.Message);
            Assert.Contains("30/09 às 10:30", erro.Message); // horário de Brasília
            Assert.Equal(1, await cenario.Db.SaidasVeiculos.CountAsync());
        }

        [Fact]
        public async Task Ticket_desconhecido_ou_invalido()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.Saida.RegistrarPorTicketAsync("K7M2-Q9XD"));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Saida.RegistrarPorTicketAsync("ABC1234"));
        }

        [Fact]
        public async Task Ticket_de_outro_carro_nao_interfere()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
            var codigoB = (await cenario.Db.EntradasVeiculos.SingleAsync(e => e.Placa == "BBB2222")).CodigoTicket!;

            var saida = await cenario.Saida.RegistrarPorTicketAsync(codigoB);

            Assert.Equal("BBB2222", saida.Placa);
            Assert.Equal(SituacaoEntrada.Ativa, (await cenario.Db.EntradasVeiculos.SingleAsync(e => e.Placa == "AAA1111")).Situacao);
        }
    }
}
