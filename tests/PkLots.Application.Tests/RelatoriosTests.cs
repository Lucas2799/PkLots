using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class RelatoriosTests
    {
        // Relógio do cenário: 30/09/2026 10:30 em Brasília
        private static readonly DateOnly Hoje = new(2026, 9, 30);

        private static Cenario CenarioComDono()
        {
            var cenario = new Cenario();
            var dono = cenario.CriarUsuario("dono@patio.com", "Senha@123", Perfis.Admin);
            dono.NomeCompleto = "Ana Dona";
            cenario.Db.SaveChanges();
            cenario.UsuarioAtualId = dono.Id;
            return cenario;
        }

        [Fact]
        public async Task Resume_receita_movimento_operadores_e_cancelamentos_do_periodo()
        {
            using var cenario = CenarioComDono();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
            var errada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("CCC3333"));
            await cenario.Entrada.CancelarAsync(errada.Id, new CancelarEntradaRequisicao("Placa digitada errada"));
            cenario.Relogio.Advance(TimeSpan.FromHours(2));
            await cenario.SairAsync("AAA1111");                       // R$ 30 em dinheiro
            await cenario.SairAsync("BBB2222", FormasPagamento.Cartao); // R$ 30 no cartão
            var mensalista = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId,
                new SalvarMensalistaRequisicao("João", null, null, null, ["DDD4444"]));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, mensalista.Id,
                new RegistrarPagamentoRequisicao(1, 250m, FormasPagamento.Pix, null));

            var relatorio = await cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje, null);

            var t = relatorio.Totais;
            Assert.Equal((2, 2, 2, 1), (t.Entradas, t.Saidas, t.SaidasCobradas, t.Cancelamentos));
            Assert.Equal((60m, 250m, 310m, 30m), (t.ReceitaAvulsos, t.ReceitaMensalidades, t.ReceitaTotal, t.TicketMedio));
            Assert.Equal("2h 0m", t.PermanenciaMedia);
            Assert.Equal([("pix", 1, 250m), ("cartao", 1, 30m), ("dinheiro", 1, 30m)], relatorio.PorForma.Select(f => (f.Forma, f.Quantidade, f.Total)));

            var dia = Assert.Single(relatorio.PorDia);
            Assert.Equal((Hoje, 310m), (dia.Data, dia.Receita));
            Assert.Equal(2, relatorio.EntradasPorHora.Single(h => h.Hora == 10).Entradas); // 10h30 em Brasília

            var patio = Assert.Single(relatorio.PorPatio);
            Assert.Equal((30, 2, 60m, 250m), (patio.Vagas, patio.Saidas, patio.ReceitaAvulsos, patio.ReceitaMensalidades));

            var operador = Assert.Single(relatorio.PorOperador);
            Assert.Equal(("Ana Dona", 2, 1, 310m, 1), (operador.Nome, operador.Saidas, operador.Mensalidades, operador.Recebido, operador.Cancelamentos));

            var cancelamento = Assert.Single(relatorio.Cancelamentos);
            Assert.Equal(("CCC3333", "Ana Dona", "Placa digitada errada"), (cancelamento.Placa, cancelamento.CanceladaPor, cancelamento.Motivo));
            Assert.Equal((1, 0, 1, 1, 1), (relatorio.Mensalistas.Ativos, relatorio.Mensalistas.Vencidos, relatorio.Mensalistas.EmDia,
                relatorio.Mensalistas.Novos, relatorio.Mensalistas.Pagamentos));
        }

        [Fact]
        public async Task Diferenca_de_caixa_aparece_no_operador()
        {
            using var cenario = CenarioComDono();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            cenario.Relogio.Advance(TimeSpan.FromHours(1));
            await cenario.SairAsync("AAA1111"); // R$ 15 em dinheiro, caixa aberto com troco zero
            await cenario.Caixa.FecharAsync(new FecharCaixaRequisicao(10m, "faltou"));

            var operador = Assert.Single((await cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje, null)).PorOperador);

            Assert.Equal((1, -5m), (operador.CaixasFechados, operador.DiferencaCaixa));
        }

        [Fact]
        public async Task Saida_cobrada_sem_forma_registrada_entra_como_nao_informada()
        {
            using var cenario = CenarioComDono();
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            cenario.Db.SaidasVeiculos.Add(new()
            {
                PatioId = cenario.Patio.Id, EntradaVeiculoId = entrada.Id, Placa = "AAA1111",
                DataHoraSaida = Cenario.Inicio.UtcDateTime.AddHours(1), ValorTotal = 15m // registro antigo, sem caixa
            });
            await cenario.Db.SaveChangesAsync();

            var relatorio = await cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje, null);

            var forma = Assert.Single(relatorio.PorForma);
            Assert.Equal((ServicoRelatorios.FormaNaoInformada, 15m), (forma.Forma, forma.Total));
            Assert.Empty(relatorio.PorOperador);
        }

        [Fact]
        public async Task Dias_seguem_o_fuso_de_brasilia()
        {
            using var cenario = CenarioComDono();
            // 30/09 23:30 em Brasília = 01/10 02:30 UTC
            cenario.Relogio.SetUtcNow(new DateTimeOffset(2026, 10, 1, 2, 30, 0, TimeSpan.Zero));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));

            var relatorio = await cenario.Relatorios.ObterDaEmpresaAsync(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1), null);

            Assert.Equal([1, 0], relatorio.PorDia.Select(d => d.Entradas));
            Assert.Equal(1, relatorio.EntradasPorHora.Single(h => h.Hora == 23).Entradas);
        }

        [Fact]
        public async Task Sem_datas_usa_o_mes_atual_e_recusa_periodo_invalido()
        {
            using var cenario = CenarioComDono();

            var relatorio = await cenario.Relatorios.ObterDaEmpresaAsync(null, null, null);
            Assert.Equal((new DateOnly(2026, 9, 1), Hoje), (relatorio.De, relatorio.Ate));
            Assert.Equal(30, relatorio.PorDia.Count);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje.AddDays(-1), null));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Relatorios.ObterDaEmpresaAsync(Hoje.AddDays(-400), Hoje, null));
        }

        [Fact]
        public async Task Dono_nao_ve_patio_nem_movimento_de_outra_empresa()
        {
            using var cenario = CenarioComDono();
            var (_, outroPatio) = cenario.CriarOutraEmpresa();
            cenario.Db.EntradasVeiculos.Add(new() { PatioId = outroPatio.Id, Placa = "ZZZ9999", DataHoraEntrada = Cenario.Inicio.UtcDateTime });
            await cenario.Db.SaveChangesAsync();

            var relatorio = await cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje, null);

            Assert.Equal(0, relatorio.Totais.Entradas);
            Assert.Equal(["Pátio Teste"], relatorio.PorPatio.Select(p => p.Nome));
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje, outroPatio.Id));
        }

        [Fact]
        public async Task Filtra_por_um_patio()
        {
            using var cenario = CenarioComDono();
            var shopping = await cenario.Patios.CriarAsync(cenario.UsuarioAtualId, new CriarPatioRequisicao("Shopping", 10, 15m, 0));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));

            var relatorio = await cenario.Relatorios.ObterDaEmpresaAsync(Hoje, Hoje, shopping.Id);

            Assert.Equal("Shopping", relatorio.NomePatio);
            Assert.Equal(0, relatorio.Totais.Entradas);
            Assert.Equal(["Shopping"], relatorio.PorPatio.Select(p => p.Nome));
        }
    }
}
