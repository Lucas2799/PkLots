using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;

namespace PkLots.Application.Tests
{
    public class ServicoMotoristaTests
    {
        [Fact]
        public async Task Mostra_estacionamento_atual_com_valor_estimado()
        {
            using var cenario = new Cenario();
            var motorista = cenario.CriarUsuario("driver@example.com", "Driver@123");
            await cenario.Motorista.CadastrarVeiculoAsync(motorista.Id, new CadastrarVeiculoRequisicao("ABC1234", "Onix", "Prata"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromHours(2));

            var atual = await cenario.Motorista.ObterEstacionamentoAtualAsync(motorista.Id);

            Assert.NotNull(atual);
            Assert.Equal("A1", atual.NumeroVaga);
            Assert.Equal("2h 0m", atual.Duracao);
            Assert.Equal(30.00m, atual.ValorEstimado);
        }

        [Fact]
        public async Task Sem_veiculo_estacionado_retorna_nulo()
        {
            using var cenario = new Cenario();
            var motorista = cenario.CriarUsuario("driver@example.com", "Driver@123");

            Assert.Null(await cenario.Motorista.ObterEstacionamentoAtualAsync(motorista.Id));
        }

        [Fact]
        public async Task Nao_vincula_veiculo_de_outro_motorista()
        {
            using var cenario = new Cenario();
            var primeiro = cenario.CriarUsuario("um@example.com", "Senha@123");
            var segundo = cenario.CriarUsuario("dois@example.com", "Senha@123");
            await cenario.Motorista.CadastrarVeiculoAsync(primeiro.Id, new CadastrarVeiculoRequisicao("ABC1234", null, null));

            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Motorista.CadastrarVeiculoAsync(segundo.Id, new CadastrarVeiculoRequisicao("ABC1234", null, null)));
        }


    }
}
