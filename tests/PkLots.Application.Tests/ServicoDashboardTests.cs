using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Application.Tests
{
    public class ServicoDashboardTests
    {
        [Fact]
        public async Task Resume_movimento_do_dia_e_veiculos_estacionados()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
            cenario.Relogio.Advance(TimeSpan.FromHours(1));
            await cenario.SairAsync("AAA1111");

            var dashboard = await cenario.Dashboard.ObterAsync();

            Assert.Equal(2, dashboard.Hoje.Entradas);
            Assert.Equal(1, dashboard.Hoje.Saidas);
            Assert.Equal(15.00m, dashboard.Hoje.Receita);
            Assert.Equal("1h 0m", dashboard.Hoje.MediaEstadia);
            var estacionado = Assert.Single(dashboard.Estacionados);
            Assert.Equal("BBB2222", estacionado.Placa);
            Assert.Equal(24, dashboard.Ocupacao24h.Count);
            // Último ponto = agora (14:30:45): AAA1111 acabou de sair, só BBB2222 segue no pátio
            Assert.Equal(1, dashboard.Ocupacao24h[^1].Ocupadas);
            Assert.Equal(Cenario.Inicio.UtcDateTime.AddHours(1), dashboard.Ocupacao24h[^1].Hora);
            // Ponto das 14:00: os dois veículos estavam no pátio
            Assert.Equal(2, dashboard.Ocupacao24h[^2].Ocupadas);
        }

        [Fact]
        public void Inicio_do_dia_usa_fuso_de_brasilia()
        {
            // 02:00 UTC de 01/10 ainda é 30/09 em Brasília (UTC-3)
            var inicio = FusoHorario.InicioDoDiaUtc(new DateTime(2026, 10, 1, 2, 0, 0, DateTimeKind.Utc));

            Assert.Equal(new DateTime(2026, 9, 30, 3, 0, 0, DateTimeKind.Utc), inicio);
        }
    }
}
