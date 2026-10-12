using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    // Relógio do cenário: 30/09/2026 10:30 em Brasília; a assinatura da empresa começou em 29/09
    public class FaturasTests
    {
        private static readonly Guid SuperAdmin = Guid.NewGuid();
        private static readonly DateOnly Hoje = new(2026, 9, 30);
        private const string ChavePix = "pagamentos@pklots.com.br";

        private static async Task<Cenario> CenarioComPlanoAsync(int diaVencimento = 5, decimal preco = 100m, DateOnly? testeAte = null)
        {
            var cenario = new Cenario();
            cenario.CriarUsuario("dono@empresa.com", "x", Perfis.Admin);
            var plano = await cenario.Planos.CriarAsync(new SalvarPlanoRequisicao("Profissional", null, preco, null, null, null, []));
            await cenario.Planos.SalvarAssinaturaAsync(SuperAdmin, cenario.Empresa.Id,
                new SalvarAssinaturaRequisicao(plano.Id, null, diaVencimento, testeAte, null));
            return cenario;
        }

        [Fact]
        public async Task Emite_a_mensalidade_na_janela_avisa_o_dono_e_nao_duplica()
        {
            using var cenario = await CenarioComPlanoAsync(diaVencimento: 5);
            await cenario.Patios.CriarAsync(Guid.NewGuid(), new CriarPatioRequisicao("Shopping", 10));

            Assert.Equal(1, (await cenario.Faturas.EmitirMensalidadesAsync()).Emitidas);
            Assert.Equal(0, (await cenario.Faturas.EmitirMensalidadesAsync()).Emitidas);

            // Setembro venceu (05/09) antes de virar cliente; outubro (05/10) já está na janela de 10 dias
            var fatura = Assert.Single(await cenario.Faturas.ListarAsync(null, null));
            Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5), 200m), (fatura.Competencia, fatura.Vencimento, fatura.Valor));
            Assert.Equal((2, 100m, "Profissional", SituacaoFatura.Aberta), (fatura.PatiosCobrados, fatura.PrecoPorPatio, fatura.NomePlano, fatura.Situacao));
            var email = await cenario.Db.EmailsPendentes.SingleAsync();
            Assert.Equal("dono@empresa.com", email.Para);
            Assert.Contains("R$ 200,00", email.CorpoTexto);
        }

        [Fact]
        public async Task So_emite_a_partir_de_10_dias_antes_do_vencimento()
        {
            using var cenario = await CenarioComPlanoAsync(diaVencimento: 20);

            Assert.Equal(0, (await cenario.Faturas.EmitirMensalidadesAsync()).Emitidas);
            cenario.Relogio.SetUtcNow(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
            Assert.Equal(1, (await cenario.Faturas.EmitirMensalidadesAsync()).Emitidas);
            Assert.Equal(new DateOnly(2026, 10, 20), (await cenario.Faturas.ListarAsync(null, null)).Single().Vencimento);
        }

        [Fact]
        public async Task Periodo_de_teste_e_plano_gratuito_nao_geram_fatura()
        {
            using var emTeste = await CenarioComPlanoAsync(testeAte: new DateOnly(2026, 10, 31));
            Assert.Equal(0, (await emTeste.Faturas.EmitirMensalidadesAsync()).Emitidas);

            using var gratuito = await CenarioComPlanoAsync(preco: 0m);
            Assert.Equal(0, (await gratuito.Faturas.EmitirMensalidadesAsync()).Emitidas);
        }

        [Fact]
        public async Task Fatura_aberta_traz_o_pix_do_pklots_com_o_valor()
        {
            using var cenario = await CenarioComPlanoAsync();
            await cenario.Faturas.SalvarConfiguracaoAsync(new ConfiguracaoPlataformaDto(ChavePix, "PkLots Sistemas", "Sao Paulo"));
            await cenario.Faturas.EmitirMensalidadesAsync();

            var pix = (await cenario.Faturas.ListarDaMinhaEmpresaAsync()).Single().PixCopiaECola;

            Assert.NotNull(pix);
            Assert.Contains(ChavePix, pix);
            Assert.Contains("5406100.00", pix); // campo 54: valor
            Assert.Contains("copia e cola", (await cenario.Db.EmailsPendentes.SingleAsync()).CorpoTexto);
            await Assert.ThrowsAsync<ExcecaoNegocio>(() =>
                cenario.Faturas.SalvarConfiguracaoAsync(new ConfiguracaoPlataformaDto("chave-invalida", "PkLots", "SP")));
        }

        [Fact]
        public async Task Baixa_manual_marca_paga_e_tira_o_pix()
        {
            using var cenario = await CenarioComPlanoAsync();
            await cenario.Faturas.SalvarConfiguracaoAsync(new ConfiguracaoPlataformaDto(ChavePix, "PkLots Sistemas", "Sao Paulo"));
            await cenario.Faturas.EmitirMensalidadesAsync();
            var aberta = (await cenario.Faturas.ListarAsync(null, null)).Single();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Faturas.BaixarAsync(SuperAdmin, aberta.Id,
                new BaixarFaturaRequisicao(FormasPagamento.Pix, Hoje.AddDays(1))));
            var paga = await cenario.Faturas.BaixarAsync(SuperAdmin, aberta.Id, new BaixarFaturaRequisicao(FormasPagamento.Pix, Hoje.AddDays(-1)));

            Assert.Equal((SituacaoFatura.Paga, 100m, (string?)null), (paga.Situacao, paga.ValorPago, paga.PixCopiaECola));
            Assert.Equal(new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc), paga.PagaEm); // meio-dia em Brasília
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Faturas.BaixarAsync(SuperAdmin, aberta.Id,
                new BaixarFaturaRequisicao(FormasPagamento.Pix)));
        }

        [Fact]
        public async Task Vencida_e_calculada_e_cancelar_exige_motivo()
        {
            using var cenario = await CenarioComPlanoAsync();
            await cenario.Faturas.EmitirMensalidadesAsync();
            cenario.Relogio.SetUtcNow(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

            var vencida = Assert.Single(await cenario.Faturas.ListarAsync(null, SituacaoFatura.Vencida));
            Assert.Equal((SituacaoFatura.Vencida, 3), (vencida.Situacao, vencida.DiasEmAtraso));

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Faturas.CancelarAsync(SuperAdmin, vencida.Id, new CancelarFaturaRequisicao(" ")));
            var cancelada = await cenario.Faturas.CancelarAsync(SuperAdmin, vencida.Id, new CancelarFaturaRequisicao("Cortesia do mês"));
            Assert.Equal(SituacaoFatura.Cancelada, cancelada.Situacao);
            Assert.Empty(await cenario.Faturas.ListarDaMinhaEmpresaAsync()); // o dono não vê canceladas
        }

        [Fact]
        public async Task Avulsa_e_lancada_para_a_empresa_e_o_dono_so_ve_as_suas()
        {
            using var cenario = await CenarioComPlanoAsync();
            var (outra, _) = cenario.CriarOutraEmpresa();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Faturas.LancarAvulsaAsync(cenario.Empresa.Id,
                new LancarFaturaRequisicao("Implantação", 500m, Hoje.AddDays(-1))));
            await cenario.Faturas.LancarAvulsaAsync(cenario.Empresa.Id, new LancarFaturaRequisicao("Implantação", 500m, Hoje.AddDays(7)));
            await cenario.Faturas.LancarAvulsaAsync(outra.Id, new LancarFaturaRequisicao("Implantação", 300m, Hoje.AddDays(7)));

            var minhas = await cenario.Faturas.ListarDaMinhaEmpresaAsync();
            Assert.Equal([(TipoFatura.Avulsa, 500m)], minhas.Select(f => (f.Tipo, f.Valor)));
            Assert.Equal(2, (await cenario.Faturas.ListarAsync(null, null)).Count);
        }
    }
}
