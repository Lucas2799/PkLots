using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Pagamentos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class PixCopiaEColaTests
    {
        [Fact]
        public void Crc16_segue_o_padrao_ccitt_false() =>
            Assert.Equal(0x29B1, PixCopiaECola.Crc16("123456789")); // valor de verificação do CRC-16/CCITT-FALSE

        [Fact]
        public void Gera_payload_no_padrao_do_banco_central()
        {
            var pix = PixCopiaECola.Gerar("pagamentos@patio.com.br", "Estacionamento São João Ltda ME", "São Paulo", 63.75m, "PKL-123 abc");

            Assert.StartsWith("000201", pix);
            Assert.Contains("0014br.gov.bcb.pix0123pagamentos@patio.com.br", pix);
            Assert.Contains("5303986", pix);           // real
            Assert.Contains("540563.75", pix);         // valor com ponto
            Assert.Contains("5802BR", pix);
            Assert.Contains("5925ESTACIONAMENTO SAO JOAO", pix); // sem acento, até 25 caracteres
            Assert.Contains("6009SAO PAULO", pix);
            Assert.Contains("62130509PKL123abc", pix); // txid só com letras/números
            // CRC confere com o conteúdo
            Assert.Equal(PixCopiaECola.Crc16(pix[..^4]).ToString("X4"), pix[^4..]);
        }

        [Theory]
        [InlineData("123.456.789-09", "12345678909")]
        [InlineData("11.222.333/0001-81", "11222333000181")]
        [InlineData("Pagamentos@Patio.com", "pagamentos@patio.com")]
        [InlineData("+5511999998888", "+5511999998888")]
        [InlineData("1f0e4a7c-8b2d-4c1e-9a3b-5d6e7f8a9b0c", "1f0e4a7c-8b2d-4c1e-9a3b-5d6e7f8a9b0c")]
        public void Aceita_tipos_de_chave(string chave, string esperado) => Assert.Equal(esperado, PixCopiaECola.ValidarChave(chave));

        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("11999998888")]   // telefone sem +55 (dígito verificador de CPF não confere)
        [InlineData("12345678900")]   // CPF com dígito errado
        [InlineData("11.222.333/0001-80")]
        [InlineData("email@")]
        public void Rejeita_chaves_invalidas(string chave) => Assert.Null(PixCopiaECola.ValidarChave(chave));
    }

    public class ConfiguracaoPagamentoTests
    {
        private static readonly Guid Admin = Guid.NewGuid();

        private static AtualizarConfiguracaoPagamentoRequisicao Pix(string chave = "pagamentos@patio.com") =>
            new(ProvedoresPagamento.PixManual, chave, "Estacionamento Centro", "Sao Paulo", true, true, true);

        [Fact]
        public async Task Padrao_aceita_dinheiro_e_cartao_e_pix_so_com_chave()
        {
            using var cenario = new Cenario();
            var padrao = await cenario.ConfiguracaoPagamento.ObterAsync();
            Assert.Null(padrao.ChavePix);

            var config = await cenario.ConfiguracaoPagamento.AtualizarAsync(Admin, Pix());
            Assert.Equal("pagamentos@patio.com", config.ChavePix);
            Assert.Contains("0120pagamentos@patio.com", await cenario.ConfiguracaoPagamento.GerarPixDeTesteAsync());
        }

        [Fact]
        public async Task Recusa_configuracao_invalida()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.ConfiguracaoPagamento.AtualizarAsync(Admin, Pix("chave-ruim")));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.ConfiguracaoPagamento.AtualizarAsync(Admin, Pix() with { Provedor = "mercadopago" }));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.ConfiguracaoPagamento.AtualizarAsync(Admin, Pix() with { NomeRecebedor = "" }));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.ConfiguracaoPagamento.AtualizarAsync(Admin,
                Pix() with { AceitaDinheiro = false, AceitaPix = false, AceitaCartao = false }));
        }

        [Fact]
        public async Task Configuracao_de_uma_empresa_nao_aparece_para_outra()
        {
            using var cenario = new Cenario();
            await cenario.ConfiguracaoPagamento.AtualizarAsync(Admin, Pix());
            var (outra, _) = cenario.CriarOutraEmpresa();

            using var db = cenario.DbDaEmpresa(outra.Id);
            Assert.Empty(await db.ConfiguracoesPagamento.ToListAsync());
        }
    }

    public class CobrancaSaidaTests
    {
        private static async Task<Cenario> CenarioComCarro(string placa = "ABC1234", TimeSpan? permanencia = null)
        {
            var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao(placa));
            cenario.Relogio.Advance(permanencia ?? TimeSpan.FromHours(2));
            return cenario;
        }

        private static Task<CobrancaDto> Cobrar(Cenario c, string placa = "ABC1234") =>
            c.Saida.CobrarAsync(new GerarCobrancaRequisicao(placa, null));

        [Fact]
        public async Task Sem_pagamento_confirmado_o_carro_continua_no_patio()
        {
            using var cenario = await CenarioComCarro();

            var cobranca = await Cobrar(cenario);

            Assert.True(cobranca.RequerPagamento);
            Assert.Equal(30.00m, cobranca.Valor);
            Assert.Equal(SituacaoEntrada.Ativa, (await cenario.Db.EntradasVeiculos.SingleAsync()).Situacao);
            Assert.Empty(await cenario.Db.SaidasVeiculos.ToListAsync());
        }

        [Fact]
        public async Task Confirmar_exige_caixa_aberto_do_operador()
        {
            using var cenario = await CenarioComCarro();
            var cobranca = await Cobrar(cenario);

            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, new ConfirmarSaidaRequisicao("dinheiro", null)));
            Assert.Contains("Abra o seu caixa", erro.Message);
        }

        [Fact]
        public async Task Dinheiro_calcula_troco_e_entra_no_caixa()
        {
            using var cenario = await CenarioComCarro();
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(50m));
            var cobranca = await Cobrar(cenario);

            var saida = await cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, new ConfirmarSaidaRequisicao("dinheiro", 50m));

            Assert.Equal((30.00m, 50m, 20m, "dinheiro", SituacaoPagamento.Pago), (saida.ValorTotal, saida.ValorRecebido, saida.Troco, saida.FormaPagamento, saida.SituacaoPagamento));
            var caixa = (await cenario.Caixa.ObterAtualAsync())!;
            Assert.Equal((1, 30m, 80m), (caixa.Saidas, caixa.TotalRecebido, caixa.DinheiroEsperado)); // 50 de troco + 30
        }

        [Fact]
        public async Task Recusa_dinheiro_insuficiente_forma_nao_aceita_e_confirmacao_dupla()
        {
            using var cenario = await CenarioComCarro();
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            var cobranca = await Cobrar(cenario);
            var confirmar = (string forma, decimal? recebido) =>
                cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, new ConfirmarSaidaRequisicao(forma, recebido));

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => confirmar("dinheiro", 20m));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => confirmar("pix", null)); // sem chave PIX cadastrada
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => confirmar("cheque", null));

            await confirmar("cartao", null);
            await Assert.ThrowsAsync<ExcecaoConflito>(() => confirmar("cartao", null));
            Assert.Single(await cenario.Db.SaidasVeiculos.ToListAsync());
        }

        [Fact]
        public async Task Valor_fica_congelado_enquanto_a_cobranca_vale_e_expira_depois()
        {
            using var cenario = await CenarioComCarro();
            var primeira = await Cobrar(cenario);

            cenario.Relogio.Advance(TimeSpan.FromMinutes(10)); // valor "andou", mas a cobrança ainda vale
            var mesma = await Cobrar(cenario);
            Assert.Equal((primeira.Id, 30.00m), (mesma.Id, mesma.Valor));

            cenario.Relogio.Advance(TimeSpan.FromMinutes(6)); // passou dos 15 minutos
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, primeira.Id, new ConfirmarSaidaRequisicao("cartao", null)));

            var nova = await Cobrar(cenario);
            Assert.NotEqual(primeira.Id, nova.Id);
            Assert.Equal(34.00m, nova.Valor); // 2h16m
        }

        [Fact]
        public async Task Pix_gera_copia_e_cola_com_o_valor_da_cobranca()
        {
            using var cenario = await CenarioComCarro();
            await cenario.ConfiguracaoPagamento.AtualizarAsync(Guid.NewGuid(),
                new AtualizarConfiguracaoPagamentoRequisicao("pix_manual", "pagamentos@patio.com", "Estacionamento Centro", "Sao Paulo", true, true, true));

            var cobranca = await Cobrar(cenario);

            Assert.Equal(["dinheiro", "pix", "cartao"], cobranca.FormasAceitas);
            Assert.Contains("540530.00", cobranca.PixCopiaECola);
            Assert.Contains($"05{cobranca.Id.ToString("N")[..22].Length + 3:00}PKL", cobranca.PixCopiaECola);
        }

        [Fact]
        public async Task Valor_zero_libera_sem_caixa_e_sem_forma()
        {
            using var cenario = await CenarioComCarro(permanencia: TimeSpan.FromMinutes(5));
            await cenario.Administracao.AtualizarConfiguracaoAsync(Guid.NewGuid(), new AtualizarConfiguracaoRequisicao("Pátio Teste", 15m, 10));

            var cobranca = await Cobrar(cenario);
            var saida = await cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, new ConfirmarSaidaRequisicao(null, null));

            Assert.False(cobranca.RequerPagamento);
            Assert.Empty(cobranca.FormasAceitas);
            Assert.Equal((0m, SituacaoPagamento.Isento, (string?)null), (saida.ValorTotal, saida.SituacaoPagamento, saida.FormaPagamento));
        }

        [Fact]
        public async Task Caixa_aberto_em_outro_patio_nao_recebe()
        {
            using var cenario = await CenarioComCarro();
            var shopping = await cenario.Patios.CriarAsync(Guid.NewGuid(), new CriarPatioRequisicao("Pátio Shopping", 5));
            cenario.PatioEscolhidoId = shopping.Id;
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m)); // caixa no shopping
            cenario.PatioEscolhidoId = cenario.Patio.Id;
            var cobranca = await Cobrar(cenario);

            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, new ConfirmarSaidaRequisicao("cartao", null)));
            Assert.Contains("Pátio Shopping", erro.Message);
        }
    }

    public class CaixaTests
    {
        [Fact]
        public async Task Fluxo_do_turno_com_sangria_suprimento_e_diferenca_no_fechamento()
        {
            using var cenario = new Cenario();
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(100m));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
            cenario.Relogio.Advance(TimeSpan.FromHours(1));
            await cenario.SairAsync("AAA1111", "dinheiro", 20m);   // R$ 15, troco 5
            await cenario.SairAsync("BBB2222", "cartao");           // R$ 15 no cartão
            await cenario.Caixa.MovimentarAsync(new MovimentarCaixaRequisicao("suprimento", 50m, "troco extra"));
            await cenario.Caixa.MovimentarAsync(new MovimentarCaixaRequisicao("sangria", 100m, "cofre"));

            var aberto = (await cenario.Caixa.ObterAtualAsync())!;
            Assert.Equal(65m, aberto.DinheiroEsperado); // 100 + 15 + 50 - 100
            Assert.Equal(30m, aberto.TotalRecebido);
            Assert.Equal([("cartao", 1, 15m), ("dinheiro", 1, 15m)], aberto.Recebimentos.Select(r => (r.Forma, r.Quantidade, r.Total)));

            var fechado = await cenario.Caixa.FecharAsync(new FecharCaixaRequisicao(62.50m, "faltou troco"));

            Assert.NotNull(fechado.FechadoEm);
            Assert.Equal((65m, 62.50m, -2.50m), (fechado.DinheiroEsperado, fechado.DinheiroContado!.Value, fechado.Diferenca!.Value));
            Assert.Null(await cenario.Caixa.ObterAtualAsync());
            Assert.True(await cenario.Db.LogsAuditoria.AnyAsync(l => l.Acao == AcoesAuditoria.CaixaFechado && l.Situacao == SituacaoAuditoria.Alerta));
        }

        [Fact]
        public async Task Um_caixa_aberto_por_operador_e_sangria_limitada_ao_dinheiro()
        {
            using var cenario = new Cenario();
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(30m));

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m)));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Caixa.MovimentarAsync(new MovimentarCaixaRequisicao("sangria", 31m, null)));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Caixa.MovimentarAsync(new MovimentarCaixaRequisicao("estorno", 1m, null)));
        }

        [Fact]
        public async Task Mensalidade_em_dinheiro_entra_no_caixa()
        {
            using var cenario = new Cenario();
            await cenario.Administracao.AtualizarConfiguracaoAsync(Guid.NewGuid(), new AtualizarConfiguracaoRequisicao("Pátio Teste", 15m, 0, 300m, 0));
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, new SalvarMensalistaRequisicao("João", null, null, null, ["ABC1234"]));

            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new RegistrarPagamentoRequisicao(1, null, "dinheiro", null)));

            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new RegistrarPagamentoRequisicao(1, null, "dinheiro", null));

            Assert.Equal(300m, (await cenario.Caixa.ObterAtualAsync())!.DinheiroEsperado);
        }

        [Fact]
        public async Task Relatorio_do_dia_admin_ve_todos_e_operador_so_o_seu()
        {
            using var cenario = new Cenario();
            var ana = Guid.NewGuid();
            var bruno = Guid.NewGuid();
            cenario.PerfilAtual = Perfis.Admin;
            cenario.UsuarioAtualId = ana;
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(10m));
            cenario.UsuarioAtualId = bruno;
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(20m));

            Assert.Equal(2, (await cenario.Caixa.ListarAsync(null)).Count);

            cenario.PerfilAtual = Perfis.Operador;
            cenario.Db.UsuariosPatios.Add(new Core.Entities.UsuarioPatio { UsuarioId = bruno, PatioId = cenario.Patio.Id });
            await cenario.Db.SaveChangesAsync();
            var meus = await cenario.Caixa.ListarAsync(null);
            Assert.Equal([20m], meus.Select(c => c.TrocoInicial));
        }
    }
}
