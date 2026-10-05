using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class CancelamentoEntradaTests
    {
        private static async Task<EntradaDto> EntrarAsync(Cenario cenario, string placa = "ABC1234") =>
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao(placa));

        private static void ComoOperador(Cenario cenario) =>
            (cenario.PerfilAtual, cenario.UsuarioAtualId) = (Perfis.Operador, cenario.CriarUsuario("op@teste.com", "Senha@123", Perfis.Operador).Id);

        [Fact]
        public async Task Libera_a_vaga_e_registra_motivo_e_auditoria()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromHours(2));

            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("  Saiu sem passar pelo caixa  "));

            var registro = await cenario.Db.EntradasVeiculos.SingleAsync();
            Assert.Equal(SituacaoEntrada.Cancelada, registro.Situacao);
            Assert.Equal("Saiu sem passar pelo caixa", registro.MotivoCancelamento);
            Assert.Equal(cenario.UsuarioAtualId, registro.CanceladaPorId);
            Assert.Equal(cenario.Relogio.GetUtcNow().UtcDateTime, registro.CanceladaEm);

            var vaga = await cenario.Db.Vagas.SingleAsync(v => v.Numero == "A1");
            Assert.Equal(SituacaoVaga.Disponivel, vaga.Situacao);
            Assert.Null(vaga.VeiculoAtualId);
            Assert.Empty(cenario.Db.SaidasVeiculos);

            var log = await cenario.Db.LogsAuditoria.SingleAsync(l => l.Acao == AcoesAuditoria.EntradaCancelada);
            Assert.Equal(SituacaoAuditoria.Alerta, log.Situacao);
            Assert.Equal("ABC1234", log.Placa);
            Assert.Equal(30.00m, log.Detalhes["valorNaoCobrado"]); // 2h × R$ 15
        }

        [Fact]
        public async Task Placa_pode_entrar_de_novo_apos_o_cancelamento()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario, "ABC1D23");
            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Placa digitada errada"));

            var nova = await EntrarAsync(cenario, "ABC1D23");

            Assert.Equal("A1", nova.NumeroVaga);
            var dashboard = await cenario.Dashboard.ObterAsync();
            Assert.Equal(1, dashboard.Hoje.Entradas); // a cancelada não conta
            Assert.Single(dashboard.Estacionados);
            Assert.Equal(1, dashboard.Ocupacao24h[^1].Ocupadas); // a cancelada deixou de ocupar a vaga
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Exige_motivo(string? motivo)
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao(motivo)));
            Assert.Equal(SituacaoEntrada.Ativa, (await cenario.Db.EntradasVeiculos.SingleAsync()).Situacao);
        }

        [Fact]
        public async Task Recusa_motivo_longo_demais()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Entrada.CancelarAsync(entrada.Id,
                new CancelarEntradaRequisicao(new string('x', SituacaoEntrada.TamanhoMaximoMotivo + 1))));
        }

        [Fact]
        public async Task Operador_cancela_logo_apos_a_entrada()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);
            ComoOperador(cenario);
            cenario.Relogio.Advance(TimeSpan.FromMinutes(SituacaoEntrada.MinutosCancelamentoOperador));

            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Placa digitada errada"));

            Assert.Equal(SituacaoEntrada.Cancelada, (await cenario.Db.EntradasVeiculos.SingleAsync()).Situacao);
        }

        [Fact]
        public async Task Operador_nao_cancela_depois_do_prazo_mas_admin_sim()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);
            var adminId = cenario.UsuarioAtualId;
            ComoOperador(cenario);
            cenario.Relogio.Advance(TimeSpan.FromMinutes(SituacaoEntrada.MinutosCancelamentoOperador + 1));

            await Assert.ThrowsAsync<ExcecaoProibido>(() =>
                cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Saiu sem pagar")));
            Assert.Equal(SituacaoEntrada.Ativa, (await cenario.Db.EntradasVeiculos.SingleAsync()).Situacao);

            (cenario.PerfilAtual, cenario.UsuarioAtualId) = (Perfis.Admin, adminId);
            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Saiu sem pagar"));
            Assert.Equal(SituacaoEntrada.Cancelada, (await cenario.Db.EntradasVeiculos.SingleAsync()).Situacao);
        }

        [Fact]
        public async Task Nao_cancela_entrada_ja_encerrada()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);
            await cenario.SairAsync("ABC1234");

            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Engano")));
            Assert.Equal(SituacaoEntrada.Concluida, (await cenario.Db.EntradasVeiculos.SingleAsync()).Situacao);
        }

        [Fact]
        public async Task Cancela_a_cobranca_pendente_e_impede_a_confirmacao()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromHours(1));
            var cobranca = await cenario.Saida.CobrarAsync(new GerarCobrancaRequisicao("ABC1234", null));
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));

            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Entrada em duplicidade"));

            Assert.Equal(SituacaoCobranca.Cancelada, (await cenario.Db.CobrancasSaida.SingleAsync()).Situacao);
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id,
                new ConfirmarSaidaRequisicao(FormasPagamento.Dinheiro, null)));
            Assert.Empty(cenario.Db.SaidasVeiculos);
        }

        [Fact]
        public async Task Ticket_de_entrada_cancelada_informa_o_cancelamento()
        {
            using var cenario = new Cenario();
            var entrada = await EntrarAsync(cenario);
            var codigo = (await cenario.Db.EntradasVeiculos.SingleAsync()).CodigoTicket!;
            await cenario.Entrada.CancelarAsync(entrada.Id, new CancelarEntradaRequisicao("Placa digitada errada"));

            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.SairPorTicketAsync(codigo));
            Assert.Contains("cancelada", erro.Message);
        }

        [Fact]
        public async Task Nao_cancela_entrada_de_outra_empresa()
        {
            using var cenario = new Cenario();
            var (_, outroPatio) = cenario.CriarOutraEmpresa();
            var alheia = new PkLots.Core.Entities.EntradaVeiculo
            {
                PatioId = outroPatio.Id, Placa = "XYZ9876", DataHoraEntrada = Cenario.Inicio.UtcDateTime, Situacao = SituacaoEntrada.Ativa
            };
            cenario.Db.EntradasVeiculos.Add(alheia);
            await cenario.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Entrada.CancelarAsync(alheia.Id, new CancelarEntradaRequisicao("Tentativa")));
            Assert.Equal(SituacaoEntrada.Ativa, alheia.Situacao);
        }
    }
}
