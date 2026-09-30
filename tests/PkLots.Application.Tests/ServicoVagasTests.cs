using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class ServicoVagasTests
    {
        [Fact]
        public async Task Resumo_conta_vagas_por_situacao()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            var c10 = await cenario.Db.Vagas.SingleAsync(v => v.Numero == "C10");
            await cenario.Vagas.MarcarManutencaoAsync(c10.Id, null);

            var resumo = await cenario.Vagas.ObterResumoAsync();

            Assert.Equal(30, resumo.Total);
            Assert.Equal(28, resumo.Disponiveis);
            Assert.Equal(1, resumo.Ocupadas);
            Assert.Equal(1, resumo.Manutencao);
        }

        [Fact]
        public async Task Nao_coloca_vaga_ocupada_em_manutencao()
        {
            using var cenario = new Cenario();
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Vagas.MarcarManutencaoAsync(entrada.VagaId!.Value, null));
        }

        [Fact]
        public async Task Libera_vaga_da_manutencao()
        {
            using var cenario = new Cenario();
            var a1 = await cenario.Db.Vagas.SingleAsync(v => v.Numero == "A1");
            await cenario.Vagas.MarcarManutencaoAsync(a1.Id, "limpeza");

            var vaga = await cenario.Vagas.LiberarManutencaoAsync(a1.Id);

            Assert.Equal(SituacaoVaga.Disponivel, vaga.Situacao);
            Assert.Null(vaga.Observacoes);
        }

        [Fact]
        public async Task Lista_vagas_com_placa_do_veiculo_atual()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            var vagas = await cenario.Vagas.ListarAsync();

            Assert.Equal(30, vagas.Count);
            Assert.Equal("ABC1234", vagas[0].PlacaAtual);
            Assert.Null(vagas[1].PlacaAtual);
        }

        [Fact]
        public async Task Vaga_inexistente_gera_nao_encontrado()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.Vagas.MarcarManutencaoAsync(Guid.NewGuid(), null));
        }
    }
}
