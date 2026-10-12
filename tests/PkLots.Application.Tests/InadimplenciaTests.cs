using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    // Mensalidade de outubro vence em 05/10; carência do plano: 7 dias → bloqueio a partir de 13/10
    public class InadimplenciaTests
    {
        private static readonly Guid SuperAdmin = Guid.NewGuid();

        private static async Task<Cenario> CenarioComFaturaAsync()
        {
            var cenario = new Cenario();
            cenario.CriarUsuario("dono@empresa.com", "x", Perfis.Admin);
            var plano = await cenario.Planos.CriarAsync(new SalvarPlanoRequisicao("Profissional", null, 100m, null, null, null, [.. RecursosPlano.Todos]));
            await cenario.Planos.SalvarAssinaturaAsync(SuperAdmin, cenario.Empresa.Id, new SalvarAssinaturaRequisicao(plano.Id, null, 5, null, null));
            await cenario.Faturas.EmitirMensalidadesAsync();
            return cenario;
        }

        private static void Dia(Cenario cenario, int dia) =>
            cenario.Relogio.SetUtcNow(new DateTimeOffset(2026, 10, dia, 13, 0, 0, TimeSpan.Zero)); // 10h em Brasília

        [Fact]
        public async Task Situacao_passa_de_em_dia_para_vencida_e_bloqueada_apos_a_carencia()
        {
            using var cenario = await CenarioComFaturaAsync();

            Dia(cenario, 5);
            Assert.Equal(SituacaoAssinatura.EmDia, (await cenario.Faturas.SituacaoDaMinhaEmpresaAsync()).Situacao);
            Dia(cenario, 6);
            var vencida = await cenario.Faturas.SituacaoDaMinhaEmpresaAsync();
            Assert.Equal((SituacaoAssinatura.Vencida, 1, 7, 100m), (vencida.Situacao, vencida.DiasEmAtraso, vencida.DiasParaBloqueio, vencida.ValorVencido));
            Dia(cenario, 12);
            Assert.Equal((SituacaoAssinatura.Vencida, 1), ((await cenario.Faturas.SituacaoDaMinhaEmpresaAsync()).Situacao,
                (await cenario.Faturas.SituacaoDaMinhaEmpresaAsync()).DiasParaBloqueio));
            Dia(cenario, 13);
            Assert.Equal(SituacaoAssinatura.Bloqueada, (await cenario.Faturas.SituacaoDaMinhaEmpresaAsync()).Situacao);
        }

        [Fact]
        public async Task Bloqueio_barra_entradas_mas_deixa_sair_e_libera_ao_pagar()
        {
            using var cenario = await CenarioComFaturaAsync();
            Dia(cenario, 12);
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111")); // ainda na carência

            Dia(cenario, 13);
            var erro = await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222")));
            Assert.Contains("vencida há 8 dias", erro.Message);
            await cenario.SairAsync("AAA1111"); // saída e cobrança continuam

            var fatura = (await cenario.Faturas.ListarAsync(null, null)).Single();
            await cenario.Faturas.BaixarAsync(SuperAdmin, fatura.Id, new BaixarFaturaRequisicao(FormasPagamento.Pix));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));
        }

        [Fact]
        public async Task Carencia_segue_a_do_plano()
        {
            using var cenario = await CenarioComFaturaAsync();
            var plano = (await cenario.Planos.ListarAsync()).Single(p => p.Nome == "Profissional");
            await cenario.Planos.AtualizarAsync(plano.Id, new SalvarPlanoRequisicao(plano.Nome, null, 100m, null, null, null, [], DiasCarencia: 0));

            Dia(cenario, 6);
            Assert.Equal(SituacaoAssinatura.Bloqueada, (await cenario.Faturas.SituacaoDaMinhaEmpresaAsync()).Situacao);
        }

        [Fact]
        public async Task Avisos_saem_uma_vez_por_etapa()
        {
            using var cenario = await CenarioComFaturaAsync(); // e-mail da emissão: 1
            int Emails() => cenario.Db.EmailsPendentes.Count();

            Assert.Equal(1, await cenario.Faturas.EnviarAvisosAsync()); // 30/09: vence em 5 dias → lembrete
            Assert.Contains("vence em 5 dia(s)", cenario.Db.EmailsPendentes.OrderBy(e => e.CriadoEm).AsEnumerable().Last().CorpoTexto);
            Assert.Equal(0, await cenario.Faturas.EnviarAvisosAsync());
            Dia(cenario, 6);
            Assert.Equal(1, await cenario.Faturas.EnviarAvisosAsync()); // vencida, com os dias até o bloqueio
            Assert.Contains("em 7 dia(s)", cenario.Db.EmailsPendentes.OrderBy(e => e.CriadoEm).AsEnumerable().Last().CorpoTexto);
            Dia(cenario, 14);
            Assert.Equal(1, await cenario.Faturas.EnviarAvisosAsync()); // bloqueio
            Assert.Equal(0, await cenario.Faturas.EnviarAvisosAsync());
            Assert.Equal(4, Emails());
        }

        [Fact]
        public async Task Superadmin_ve_a_situacao_na_lista_e_no_relatorio()
        {
            using var cenario = await CenarioComFaturaAsync();
            Dia(cenario, 20);
            cenario.PerfilAtual = Perfis.SuperAdmin;

            Assert.Equal(SituacaoAssinatura.Bloqueada, (await cenario.Plataforma.ListarEmpresasAsync()).Single().SituacaoCobranca);
            var relatorio = await cenario.Relatorios.ObterDaPlataformaAsync(null, null);
            Assert.Equal((100m, 100m, 1), (relatorio.Totais.ReceitaRecorrente, relatorio.Totais.ValorEmAberto, relatorio.Totais.EmpresasInadimplentes));
            var empresa = relatorio.Empresas.Single();
            Assert.Equal(("Profissional", 100m, SituacaoAssinatura.Bloqueada), (empresa.NomePlano, empresa.MensalidadePkLots, empresa.SituacaoCobranca));
        }
    }
}
