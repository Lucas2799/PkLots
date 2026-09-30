using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class ServicoEntradaTests
    {
        [Fact]
        public async Task Registra_entrada_na_primeira_vaga_livre_e_ocupa_a_vaga()
        {
            using var cenario = new Cenario();

            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("abc-1234", 0.95m, "easyocr"));

            Assert.Equal("ABC1234", entrada.Placa);
            Assert.Equal("A1", entrada.NumeroVaga);
            Assert.Equal(Cenario.Inicio.UtcDateTime, entrada.DataHoraEntrada);

            var vaga = await cenario.Db.Vagas.SingleAsync(v => v.Numero == "A1");
            Assert.Equal(SituacaoVaga.Ocupada, vaga.Situacao);
            Assert.True(vaga.Ocupada);
            Assert.NotNull(vaga.VeiculoAtualId);
            Assert.True(await cenario.Db.LogsAuditoria.AnyAsync(l => l.Acao == AcoesAuditoria.EntradaSucesso));
        }

        [Fact]
        public async Task Aloca_vagas_em_ordem_numerica_e_pula_manutencao()
        {
            using var cenario = new Cenario();
            var a2 = await cenario.Db.Vagas.SingleAsync(v => v.Numero == "A2");
            await cenario.Vagas.MarcarManutencaoAsync(a2.Id, "pintura");

            var primeira = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            var segunda = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));

            Assert.Equal("A1", primeira.NumeroVaga);
            Assert.Equal("A3", segunda.NumeroVaga);
        }

        [Fact]
        public async Task Recusa_veiculo_ja_estacionado_e_audita()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234")));

            Assert.True(await cenario.Db.LogsAuditoria.AnyAsync(l => l.Acao == AcoesAuditoria.EntradaRecusada));
            Assert.Equal(1, await cenario.Db.EntradasVeiculos.CountAsync());
        }

        [Fact]
        public async Task Recusa_entrada_com_patio_lotado()
        {
            using var cenario = new Cenario(vagasPorFileira: 1);
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("CCC3333"));

            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("DDD4444")));

            Assert.Contains("lotado", erro.Message);
        }

        [Fact]
        public async Task Recusa_placa_invalida()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("XX12")));
        }

        [Fact]
        public async Task Reaproveita_cadastro_do_veiculo_em_nova_entrada()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            await cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234"));

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            Assert.Equal(1, await cenario.Db.Veiculos.CountAsync());
            Assert.Equal(2, await cenario.Db.EntradasVeiculos.CountAsync());
        }

        [Fact]
        public async Task Entrada_por_imagem_usa_placa_reconhecida_pelo_lpr()
        {
            using var cenario = new Cenario();
            cenario.Lpr.Resultado = new ResultadoLprDto(true, "BRA2E19", 0.91m, "easyocr");

            var entrada = await cenario.Entrada.RegistrarPorImagemAsync(new MemoryStream([1, 2, 3]), "foto.jpg");

            Assert.Equal("BRA2E19", entrada.Placa);
            Assert.Equal(0.91m, entrada.Confianca);
            Assert.Equal("easyocr", entrada.MetodoReconhecimento);
        }

        [Fact]
        public async Task Falha_do_lpr_gera_excecao_e_auditoria()
        {
            using var cenario = new Cenario();
            cenario.Lpr.Resultado = new ResultadoLprDto(false, null, 0.2m, "easyocr");

            await Assert.ThrowsAsync<ExcecaoReconhecimento>(() =>
                cenario.Entrada.RegistrarPorImagemAsync(new MemoryStream([1]), "foto.jpg"));

            Assert.True(await cenario.Db.LogsAuditoria.AnyAsync(l => l.Acao == AcoesAuditoria.LprFalhou));
            Assert.Equal(0, await cenario.Db.EntradasVeiculos.CountAsync());
        }
    }
}
