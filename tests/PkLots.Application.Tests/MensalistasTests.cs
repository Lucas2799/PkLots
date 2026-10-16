using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class MensalistasTests
    {
        // Relógio do cenário: 30/09/2026 10:30 em Brasília
        private static readonly DateOnly Hoje = new(2026, 9, 30);

        private static SalvarMensalistaRequisicao Dados(string nome = "João Silva", params string[] placas) =>
            new(nome, "(11) 99999-0000", null, null, placas.Length > 0 ? placas : ["abc-1234"]);

        private static async Task<Cenario> CenarioComMensalidade(decimal mensalidade = 300m, int reservadas = 0, int vagasPorFileira = 10)
        {
            var cenario = new Cenario(vagasPorFileira);
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            await cenario.Administracao.AtualizarConfiguracaoAsync(cenario.UsuarioAtualId,
                new AtualizarConfiguracaoRequisicao("Pátio Teste", 15m, 0, mensalidade, reservadas));
            return cenario;
        }

        // ---------- Cadastro ----------

        [Fact]
        public async Task Cadastra_com_placas_normalizadas_e_comeca_vencido()
        {
            using var cenario = await CenarioComMensalidade();

            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João Silva", "abc-1234", "BRA 2E19", "ABC1234"));

            Assert.Equal(["ABC1234", "BRA2E19"], m.Placas);
            Assert.Equal(SituacaoMensalista.Vencido, m.Situacao); // ainda não pagou
            Assert.Null(m.ValidoAte);
        }

        [Fact]
        public async Task Placa_de_mensalista_ativo_nao_pode_ser_repetida_mas_de_inativo_e_transferida()
        {
            using var cenario = await CenarioComMensalidade();
            var joao = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "ABC1234"));

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("Maria", "ABC1234")));

            await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, joao.Id, Dados("João", "ABC1234") with { Ativo = false });
            var maria = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("Maria", "ABC1234"));
            Assert.Equal(["ABC1234"], maria.Placas);
            Assert.Empty((await cenario.Mensalistas.ObterAsync(joao.Id)).Placas);
        }

        [Fact]
        public async Task Adiciona_e_remove_placas_de_um_mensalista_existente()
        {
            using var cenario = await CenarioComMensalidade();
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "ABC1234"));

            var atualizado = await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, m.Id, Dados("João", "BRA2E19", "XYZ9876"));

            Assert.Equal(["BRA2E19", "XYZ9876"], atualizado.Placas);
            Assert.Equal(["BRA2E19", "XYZ9876"], (await cenario.Mensalistas.ObterAsync(m.Id)).Placas);
            Assert.Equal(2, await cenario.Db.MensalistasPlacas.CountAsync());
        }

        [Theory]
        [InlineData("")]
        [InlineData("XX")]
        public async Task Recusa_sem_placa_valida(string placa)
        {
            using var cenario = await CenarioComMensalidade();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId,
                new SalvarMensalistaRequisicao("João", null, null, null, placa == "" ? [] : [placa])));
        }

        // ---------- Pagamentos ----------

        [Fact]
        public async Task Pagamento_de_vencido_vale_a_partir_de_hoje_com_valor_da_mensalidade()
        {
            using var cenario = await CenarioComMensalidade(300m);
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados());

            var pago = await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new RegistrarPagamentoRequisicao(1, null, "pix", null));

            Assert.Equal(new DateOnly(2026, 10, 29), pago.ValidoAte); // 30/09 a 29/10
            Assert.Equal(SituacaoMensalista.EmDia, pago.Situacao);
            var pagamento = Assert.Single(await cenario.Mensalistas.ListarPagamentosAsync(m.Id));
            Assert.Equal((300m, Hoje, "pix"), (pagamento.Valor, pagamento.ValidoDe, pagamento.FormaPagamento));
        }

        [Fact]
        public async Task Pagamento_em_dia_continua_de_onde_parou()
        {
            using var cenario = await CenarioComMensalidade(300m);
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados());
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new RegistrarPagamentoRequisicao(1, null, "dinheiro", null));

            var renovado = await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new RegistrarPagamentoRequisicao(3, 800m, "cartao", "desconto trimestral"));

            Assert.Equal(new DateOnly(2027, 1, 29), renovado.ValidoAte); // 30/10 + 3 meses - 1 dia
            var ultimo = (await cenario.Mensalistas.ListarPagamentosAsync(m.Id))[0];
            Assert.Equal((800m, new DateOnly(2026, 10, 30)), (ultimo.Valor, ultimo.ValidoDe));
        }

        [Fact]
        public async Task Situacao_vencendo_nos_ultimos_dias_e_vencido_depois()
        {
            using var cenario = await CenarioComMensalidade();
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados());
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new RegistrarPagamentoRequisicao(1, null, "pix", null));

            cenario.Relogio.Advance(TimeSpan.FromDays(25)); // 25/10: faltam 4 dias
            Assert.Equal(SituacaoMensalista.Vencendo, (await cenario.Mensalistas.ObterAsync(m.Id)).Situacao);

            cenario.Relogio.Advance(TimeSpan.FromDays(5)); // 30/10
            var vencido = await cenario.Mensalistas.ObterAsync(m.Id);
            Assert.Equal((SituacaoMensalista.Vencido, -1), (vencido.Situacao, vencido.DiasParaVencer));
            Assert.Single(await cenario.Mensalistas.ListarAsync(SituacaoMensalista.Vencido));
        }

        [Fact]
        public async Task Recusa_pagamento_invalido()
        {
            using var cenario = await CenarioComMensalidade(0m); // mensalidade não configurada
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados());

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, null, "pix", null)));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(13, 100m, "pix", null)));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, 100m, "boleto", null)));

            await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, m.Id, Dados() with { Ativo = false });
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, 100m, "pix", null)));
        }

        // ---------- Entrada e saída ----------

        [Fact]
        public async Task Mensalista_em_dia_entra_identificado_e_sai_sem_cobranca()
        {
            using var cenario = await CenarioComMensalidade();
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João Silva", "ABC1234"));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, null, "pix", null));

            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromHours(5));
            var saida = await cenario.SairAsync("ABC1234");

            Assert.Equal(("João Silva", SituacaoMensalista.EmDia), (entrada.Mensalista, entrada.SituacaoMensalista));
            Assert.Equal((0m, SituacaoPagamento.Mensalista, "João Silva"), (saida.ValorTotal, saida.SituacaoPagamento, saida.Mensalista));
            Assert.Null(saida.Aviso);
        }

        [Fact]
        public async Task Mensalista_vencido_entra_e_paga_como_avulso_com_aviso()
        {
            using var cenario = await CenarioComMensalidade();
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João Silva", "ABC1234"));

            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromHours(2));
            var saida = await cenario.SairAsync("ABC1234");

            Assert.Equal(SituacaoMensalista.Vencido, entrada.SituacaoMensalista);
            Assert.Null((await cenario.Db.EntradasVeiculos.SingleAsync()).MensalistaId); // não ocupa vaga reservada
            Assert.Equal(30.00m, saida.ValorTotal);
            Assert.Contains("ainda não pagou", saida.Aviso);
        }

        [Fact]
        public async Task Quem_paga_enquanto_esta_estacionado_sai_sem_cobranca()
        {
            using var cenario = await CenarioComMensalidade();
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "ABC1234"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234")); // entrou vencido
            cenario.Relogio.Advance(TimeSpan.FromHours(3));

            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, null, "dinheiro", null));
            var saida = await cenario.SairAsync("ABC1234");

            Assert.Equal((0m, SituacaoPagamento.Mensalista), (saida.ValorTotal, saida.SituacaoPagamento));
        }

        // ---------- Vagas reservadas ----------

        [Fact]
        public async Task Avulso_nao_ocupa_as_vagas_reservadas_mas_mensalista_em_dia_sim()
        {
            using var cenario = await CenarioComMensalidade(reservadas: 1, vagasPorFileira: 1); // 3 vagas, 1 reservada
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "MEN1234"));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, null, "pix", null));

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
            var resumo = await cenario.Vagas.ObterResumoAsync();
            Assert.Equal((1, 1, 0), (resumo.Disponiveis, resumo.ReservadasLivres, resumo.DisponiveisAvulsos));

            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("CCC3333")));
            Assert.Contains("reservadas a mensalistas", erro.Message);

            var mensalista = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("MEN1234"));
            Assert.Equal("João", mensalista.Mensalista);
        }

        [Fact]
        public async Task Mensalista_estacionado_consome_a_reserva_e_libera_vaga_para_avulso()
        {
            using var cenario = await CenarioComMensalidade(reservadas: 2, vagasPorFileira: 1); // 3 vagas, 2 reservadas
            var m = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "MEN1234"));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, m.Id, new(1, null, "pix", null));

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111")); // única vaga de avulso
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222")));

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("MEN1234"));
            var resumo = await cenario.Vagas.ObterResumoAsync();
            Assert.Equal((1, 1, 0), (resumo.Disponiveis, resumo.ReservadasLivres, resumo.DisponiveisAvulsos));
        }

        [Fact]
        public async Task Configuracao_valida_vagas_reservadas()
        {
            using var cenario = await CenarioComMensalidade(reservadas: 5);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.AtualizarConfiguracaoAsync(cenario.UsuarioAtualId,
                new AtualizarConfiguracaoRequisicao("Pátio Teste", 15m, 0, 300m, 31)));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Administracao.AlterarQuantidadeVagasAsync(cenario.UsuarioAtualId, 4));

            // Requisição sem os campos de mensalista mantém os valores atuais
            var config = await cenario.Administracao.AtualizarConfiguracaoAsync(cenario.UsuarioAtualId, new AtualizarConfiguracaoRequisicao("Pátio Teste", 15m, 0));
            Assert.Equal((300m, 5), (config.ValorMensalidade, config.VagasReservadasMensalistas));
        }

        [Fact]
        public async Task Dashboard_resume_os_mensalistas()
        {
            using var cenario = await CenarioComMensalidade(reservadas: 3);
            var emDia = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "AAA1111"));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, emDia.Id, new(1, null, "pix", null));
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("Maria", "BBB2222")); // vencido
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));

            var resumo = (await cenario.Dashboard.ObterAsync()).Mensalistas!;

            Assert.Equal((2, 1, 0, 1, 1, 3, 2),
                (resumo.Ativos, resumo.EmDia, resumo.Vencendo, resumo.Vencidos, resumo.Estacionados, resumo.VagasReservadas, resumo.ReservadasLivres));
        }

        [Fact]
        public async Task Mensalistas_de_outra_empresa_ficam_invisiveis()
        {
            using var cenario = await CenarioComMensalidade();
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Dados("João", "ABC1234"));
            var (outra, _) = cenario.CriarOutraEmpresa();

            using var dbOutra = cenario.DbDaEmpresa(outra.Id);
            Assert.Empty(await dbOutra.Mensalistas.ToListAsync());
            Assert.Empty(await dbOutra.MensalistasPlacas.ToListAsync());
        }
    }
}
