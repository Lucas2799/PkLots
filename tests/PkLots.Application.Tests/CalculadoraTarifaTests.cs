using PkLots.Application.Servicos;

namespace PkLots.Application.Tests
{
    public class CalculadoraTarifaTests
    {
        private static readonly DateTime Entrada = new(2026, 9, 30, 10, 30, 45, DateTimeKind.Utc);

        [Fact]
        public void Exemplo_do_descritivo_4h14m45s_cobra_4h15m()
        {
            var saida = new DateTime(2026, 9, 30, 14, 45, 30, DateTimeKind.Utc);

            var tarifa = CalculadoraTarifa.Calcular(Entrada, saida, 15m);

            Assert.Equal(15285, tarifa.DuracaoSegundos);
            Assert.Equal("4h 15m", tarifa.DuracaoFormatada);
            Assert.Equal(4.25m, tarifa.DuracaoHoras);
            Assert.Equal(63.75m, tarifa.ValorTotal);
        }

        [Fact]
        public void Hora_exata_nao_arredonda_para_cima()
        {
            var tarifa = CalculadoraTarifa.Calcular(Entrada, Entrada.AddHours(2), 15m);

            Assert.Equal("2h 0m", tarifa.DuracaoFormatada);
            Assert.Equal(30.00m, tarifa.ValorTotal);
        }

        [Fact]
        public void Fracao_de_minuto_conta_como_minuto_inteiro()
        {
            var tarifa = CalculadoraTarifa.Calcular(Entrada, Entrada.AddSeconds(61), 15m);

            Assert.Equal(2, tarifa.MinutosCobrados);
            Assert.Equal(0.50m, tarifa.ValorTotal);
        }

        [Fact]
        public void Saida_antes_da_entrada_resulta_em_zero()
        {
            var tarifa = CalculadoraTarifa.Calcular(Entrada, Entrada.AddMinutes(-5), 15m);

            Assert.Equal(0, tarifa.DuracaoSegundos);
            Assert.Equal(0m, tarifa.ValorTotal);
        }

        [Theory]
        [InlineData(0, "0h 0m")]
        [InlineData(59, "0h 59m")]
        [InlineData(154, "2h 34m")]
        public void Formata_minutos(int minutos, string esperado) =>
            Assert.Equal(esperado, CalculadoraTarifa.FormatarMinutos(minutos));
    }
}
