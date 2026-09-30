using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class ServicoSaidaTests
    {
        [Fact]
        public async Task Calcula_valor_e_libera_a_vaga()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(new TimeSpan(4, 14, 45));

            var saida = await cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234"));

            Assert.Equal("4h 15m", saida.DuracaoFormatada);
            Assert.Equal(63.75m, saida.ValorTotal);
            Assert.Equal("A1", saida.NumeroVaga);

            var vaga = await cenario.Db.Vagas.SingleAsync(v => v.Numero == "A1");
            Assert.Equal(SituacaoVaga.Disponivel, vaga.Situacao);
            Assert.Null(vaga.VeiculoAtualId);

            var entrada = await cenario.Db.EntradasVeiculos.SingleAsync();
            Assert.Equal(SituacaoEntrada.Concluida, entrada.Situacao);
        }

        [Fact]
        public async Task Usa_valor_hora_do_patio()
        {
            using var cenario = new Cenario();
            cenario.Patio.ValorHora = 10m;
            await cenario.Db.SaveChangesAsync();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromMinutes(90));

            var saida = await cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234"));

            Assert.Equal(10m, saida.ValorHora);
            Assert.Equal(15.00m, saida.ValorTotal);
        }

        [Fact]
        public async Task Saida_sem_entrada_ativa_gera_alerta()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("XYZ9999")));

            var log = await cenario.Db.LogsAuditoria.SingleAsync();
            Assert.Equal(AcoesAuditoria.SaidaSemEntrada, log.Acao);
            Assert.Equal(SituacaoAuditoria.Alerta, log.Situacao);
        }

        [Fact]
        public async Task Nao_permite_duas_saidas_para_a_mesma_entrada()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            await cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234"));

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234")));
            Assert.Equal(1, await cenario.Db.SaidasVeiculos.CountAsync());
        }
    }
}
