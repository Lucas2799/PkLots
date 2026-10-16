using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class TabelaPrecoTests
    {
        // Horário de Brasília → UTC (quarta-feira 30/09/2026 é dia de semana; 03/10 é sábado)
        private static DateTime Local(int dia, int hora, int minuto = 0, int mes = 9) =>
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, mes, dia, hora, minuto, 0), FusoHorario.Brasil);

        private static readonly RegrasPreco Fracao = new(ModosCobranca.Fracao, PrimeiroPeriodoMinutos: 60, ValorPrimeiroPeriodo: 10m,
            FracaoMinutos: 30, ValorFracao: 3m);

        private static decimal Valor(RegrasPreco regras, DateTime entrada, DateTime saida, RegrasPreco? fimDeSemana = null, int tolerancia = 0) =>
            CalculadoraTarifa.Calcular(entrada, saida, new TabelaPreco(regras, fimDeSemana), tolerancia).ValorTotal;

        [Theory]
        [InlineData(59, 10)]   // dentro da 1ª hora
        [InlineData(60, 10)]
        [InlineData(61, 13)]   // fração iniciada é cobrada inteira
        [InlineData(90, 13)]
        [InlineData(91, 16)]
        public void Primeira_hora_mais_fracoes(int minutos, decimal esperado)
        {
            var entrada = Local(30, 10);
            Assert.Equal(esperado, Valor(Fracao, entrada, entrada.AddMinutes(minutos)));
        }

        [Fact]
        public void Detalha_o_calculo_e_respeita_a_tolerancia()
        {
            var entrada = Local(30, 10);
            var tabela = new TabelaPreco(Fracao);

            var cobrado = CalculadoraTarifa.Calcular(entrada, entrada.AddMinutes(95), tabela, 10);
            Assert.Equal("1ª hora R$ 10,00 + 2 × 30 min de R$ 3,00", cobrado.Detalhamento);

            var isento = CalculadoraTarifa.Calcular(entrada, entrada.AddMinutes(8), tabela, 10);
            Assert.Equal((0m, true), (isento.ValorTotal, isento.Isento));
        }

        [Fact]
        public void Diaria_maxima_limita_cada_24h_iniciadas()
        {
            var hora = new RegrasPreco(ValorHora: 15m, DiariaMaxima: 50m);
            var entrada = Local(30, 10);

            Assert.Equal(45m, Valor(hora, entrada, entrada.AddHours(3)));
            Assert.Equal(50m, Valor(hora, entrada, entrada.AddHours(4)));
            Assert.Equal(100m, Valor(hora, entrada, entrada.AddHours(25))); // duas diárias iniciadas
            Assert.Equal("2 diárias de R$ 50,00",
                CalculadoraTarifa.Calcular(entrada, entrada.AddHours(25), new TabelaPreco(hora)).Detalhamento);
        }

        [Fact]
        public void Pernoite_e_o_maximo_cobrado_por_noite_na_janela()
        {
            var regras = new RegrasPreco(ValorHora: 10m, ValorPernoite: 25m, InicioPernoite: 20, FimPernoite: 8);

            // 18h → 9h do dia seguinte: 3h fora da janela (R$ 30) + uma noite inteira (R$ 120 pela hora) limitada a R$ 25
            var resultado = CalculadoraTarifa.Calcular(Local(30, 18), Local(1, 9, mes: 10), new TabelaPreco(regras));
            Assert.Equal(55m, resultado.ValorTotal);
            Assert.Contains("1 pernoite(s) de R$ 25,00", resultado.Detalhamento);

            // Só 1h dentro da janela: sai mais barato pela hora, então não cobra o pernoite
            Assert.Equal(20m, Valor(regras, Local(30, 19), Local(30, 21)));
        }

        [Fact]
        public void Fim_de_semana_usa_as_regras_proprias_pelo_dia_da_entrada()
        {
            var semana = new RegrasPreco(ValorHora: 10m);
            var fimDeSemana = new RegrasPreco(ValorHora: 20m);

            Assert.Equal(20m, Valor(semana, Local(30, 10), Local(30, 12), fimDeSemana));          // quarta
            Assert.Equal(40m, Valor(semana, Local(3, 10, mes: 10), Local(3, 12, mes: 10), fimDeSemana)); // sábado
            Assert.Equal(20m, Valor(semana, Local(3, 10, mes: 10), Local(3, 12, mes: 10)));      // sem regras de fim de semana
        }

        [Fact]
        public void Valida_a_tabela()
        {
            Assert.Throws<ExcecaoNegocio>(() => new TabelaPreco(Fracao with { ValorPrimeiroPeriodo = 0, ValorFracao = 0 }).Validar());
            Assert.Throws<ExcecaoNegocio>(() => new TabelaPreco(Fracao with { FracaoMinutos = 0 }).Validar());
            Assert.Throws<ExcecaoNegocio>(() => new TabelaPreco(new RegrasPreco(ValorHora: 10.555m)).Validar());
            Assert.Throws<ExcecaoNegocio>(() => new TabelaPreco(new RegrasPreco(ValorPernoite: 20m, InicioPernoite: 8, FimPernoite: 8)).Validar());
            Assert.Throws<ExcecaoNegocio>(() => new TabelaPreco(new RegrasPreco(Modo: "diaria")).Validar());
        }

        [Fact]
        public void Descricao_resume_a_tabela_para_o_ticket()
        {
            var tabela = new TabelaPreco(Fracao with { DiariaMaxima = 60m }, new RegrasPreco(ValorHora: 12m));

            Assert.Equal("Seg. a sex.: 1ª hora R$ 10,00 · cada 30 min adicionais ou fração R$ 3,00 · diária máx. R$ 60,00. " +
                "Sáb. e dom.: R$ 12,00 por hora", tabela.Descricao());
        }

        // ---------- Pátio ----------

        [Fact]
        public async Task Patio_sem_tabela_cobra_por_hora_como_antes()
        {
            using var cenario = new Cenario();

            var tabela = await cenario.TabelaPreco.ObterAsync();

            Assert.Equal((ModosCobranca.Hora, 15m, (RegrasPreco?)null), (tabela.Semana.Modo, tabela.Semana.ValorHora, tabela.FimDeSemana));
            Assert.Equal("R$ 15,00 por hora", tabela.Descricao);
        }

        [Fact]
        public async Task Tabela_salva_vale_na_cobranca_da_saida_e_no_ticket()
        {
            using var cenario = new Cenario();
            await cenario.TabelaPreco.SalvarAsync(Guid.NewGuid(), new SalvarTabelaPrecoRequisicao(Fracao, null));
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromMinutes(95));

            var cobranca = await cenario.Saida.CobrarAsync(new GerarCobrancaRequisicao("ABC1234", null));

            Assert.Equal(16m, cobranca.Valor);
            Assert.Equal("1ª hora R$ 10,00 + 2 × 30 min de R$ 3,00", cobranca.Detalhamento);
            var ticket = await cenario.Impressao.ObterTicketEntradaAsync(entrada.Id);
            Assert.StartsWith("1ª hora R$ 10,00", ticket.DescricaoPreco);
            Assert.Equal(15m, (await cenario.Db.Patios.SingleAsync()).ValorHora); // no modo frações o valor/hora fica como estava
        }

        [Fact]
        public async Task Salvar_por_hora_atualiza_o_valor_hora_do_patio()
        {
            using var cenario = new Cenario();

            await cenario.TabelaPreco.SalvarAsync(Guid.NewGuid(), new SalvarTabelaPrecoRequisicao(new RegrasPreco(ValorHora: 18m), null));

            Assert.Equal(18m, (await cenario.Db.Patios.SingleAsync()).ValorHora);
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.TabelaPreco.SalvarAsync(Guid.NewGuid(),
                new SalvarTabelaPrecoRequisicao(Fracao with { ValorPrimeiroPeriodo = 0, ValorFracao = 0 }, null)));
        }

        [Fact]
        public async Task Simula_sem_salvar_com_a_tolerancia_do_patio()
        {
            using var cenario = new Cenario();

            var simulacao = await cenario.TabelaPreco.SimularAsync(
                new SimularTarifaRequisicao(Fracao, new RegrasPreco(ValorHora: 30m), Local(3, 10, mes: 10), Local(3, 11, 30, mes: 10)));

            Assert.Equal((45m, true), (simulacao.Valor, simulacao.FimDeSemana));
            Assert.Null((await cenario.Db.Patios.SingleAsync()).TabelaPrecoJson);
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.TabelaPreco.SimularAsync(
                new SimularTarifaRequisicao(Fracao, null, Local(3, 10, mes: 10), Local(3, 9, mes: 10))));
        }

        [Fact]
        public async Task Tolerancia_simulada_e_salva_junto_com_a_tabela()
        {
            using var cenario = new Cenario();
            var entrada = Local(1, 10, mes: 10);

            var simulacao = await cenario.TabelaPreco.SimularAsync(
                new SimularTarifaRequisicao(Fracao, null, entrada, entrada.AddMinutes(12), MinutosTolerancia: 15));
            Assert.Equal((0m, true), (simulacao.Valor, simulacao.Isento));

            var salva = await cenario.TabelaPreco.SalvarAsync(Guid.NewGuid(), new SalvarTabelaPrecoRequisicao(Fracao, null, 15));
            Assert.Equal(15, salva.MinutosTolerancia);
            Assert.Equal(15, (await cenario.Db.Patios.SingleAsync()).MinutosTolerancia);

            // Sem tolerância na requisição, mantém a atual
            await cenario.TabelaPreco.SalvarAsync(Guid.NewGuid(), new SalvarTabelaPrecoRequisicao(Fracao, null));
            Assert.Equal(15, (await cenario.Db.Patios.SingleAsync()).MinutosTolerancia);
            await Assert.ThrowsAsync<ExcecaoNegocio>(() =>
                cenario.TabelaPreco.SalvarAsync(Guid.NewGuid(), new SalvarTabelaPrecoRequisicao(Fracao, null, 241)));
        }
    }
}
