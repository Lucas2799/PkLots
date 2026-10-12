using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class PlanosTests
    {
        private static readonly Guid SuperAdmin = Guid.NewGuid();
        private static readonly Guid Admin = Guid.NewGuid();

        private static SalvarPlanoRequisicao Basico(params string[] recursos) =>
            new("Básico", "Pátio pequeno", 99m, MaximoPatios: 1, MaximoVagasPorPatio: 40, MaximoUsuarios: 2, recursos);

        // Coloca a empresa do cenário num plano novo
        private static async Task<PlanoDto> AssinarAsync(Cenario cenario, SalvarPlanoRequisicao plano)
        {
            var criado = await cenario.Planos.CriarAsync(plano);
            await cenario.Planos.SalvarAssinaturaAsync(SuperAdmin, cenario.Empresa.Id, new SalvarAssinaturaRequisicao(criado.Id, null, 10, null, null));
            return criado;
        }

        [Fact]
        public async Task Superadmin_cria_e_edita_plano_com_recursos_do_catalogo()
        {
            using var cenario = new Cenario();

            var plano = await cenario.Planos.CriarAsync(Basico(RecursosPlano.Relatorios, "LPR", RecursosPlano.Relatorios));
            Assert.Equal([RecursosPlano.Lpr, RecursosPlano.Relatorios], plano.Recursos); // normalizado e na ordem do catálogo
            Assert.Equal(7, plano.DiasCarencia);

            var editado = await cenario.Planos.AtualizarAsync(plano.Id, Basico() with { PrecoPorPatio = 120m, MaximoUsuarios = null, DiasCarencia = 10 });
            Assert.Equal((120m, (int?)null, 10), (editado.PrecoPorPatio, editado.MaximoUsuarios, editado.DiasCarencia));
            Assert.Empty(editado.Recursos);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Planos.CriarAsync(Basico("pix_automatico") with { Nome = "Outro" }));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Planos.CriarAsync(Basico() with { Nome = "Outro", MaximoPatios = 0 }));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Planos.CriarAsync(Basico() with { Nome = "básico" }));
        }

        [Fact]
        public async Task Assinatura_calcula_valor_por_patio_com_preco_negociado()
        {
            using var cenario = new Cenario();
            var plano = await AssinarAsync(cenario, Basico() with { MaximoPatios = null });
            await cenario.Patios.CriarAsync(Admin, new CriarPatioRequisicao("Shopping", 10));

            var assinatura = await cenario.Planos.SalvarAssinaturaAsync(SuperAdmin, cenario.Empresa.Id,
                new SalvarAssinaturaRequisicao(plano.Id, 80m, 5, new DateOnly(2026, 10, 31), "Piloto"));

            Assert.Equal(("Básico", 2, 160m, 5), (assinatura.NomePlano, assinatura.PatiosAtivos, assinatura.ValorMensal, assinatura.DiaVencimento));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Planos.SalvarAssinaturaAsync(SuperAdmin, cenario.Empresa.Id,
                new SalvarAssinaturaRequisicao(plano.Id, null, 29, null, null)));
        }

        [Fact]
        public async Task Limites_de_patios_vagas_e_usuarios_barram_so_o_que_excede()
        {
            using var cenario = new Cenario();
            await AssinarAsync(cenario, Basico());
            cenario.CriarUsuario("admin@empresa.com", "x", Perfis.Admin);

            // 1 pátio: o do cenário
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Patios.CriarAsync(Admin, new CriarPatioRequisicao("Shopping", 10)));
            // 40 vagas por pátio: o pátio tem 30; aumentar até 40 pode, além não; reduzir sempre pode
            await cenario.Administracao.AlterarQuantidadeVagasAsync(Admin, 40);
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Administracao.AlterarQuantidadeVagasAsync(Admin, 41));
            await cenario.Administracao.AlterarQuantidadeVagasAsync(Admin, 20);
            // 2 usuários: há 1 admin; o 2º entra, o 3º não
            await cenario.Administracao.CriarUsuarioAsync(Admin, new CriarUsuarioRequisicao("op1@empresa.com", "Op Um", Perfis.Operador, [cenario.Patio.Id]));
            var erro = await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Administracao.CriarUsuarioAsync(Admin,
                new CriarUsuarioRequisicao("op2@empresa.com", "Op Dois", Perfis.Operador, [cenario.Patio.Id])));
            Assert.Contains("até 2 usuário(s)", erro.Message);
        }

        [Fact]
        public async Task Reativar_usuario_respeita_o_limite()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("admin@empresa.com", "x", Perfis.Admin);
            var inativo = cenario.CriarUsuario("op@empresa.com", "x", Perfis.Operador);
            inativo.Ativo = false;
            cenario.CriarUsuario("op2@empresa.com", "x", Perfis.Operador);
            await AssinarAsync(cenario, Basico()); // 2 ativos = no limite

            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Administracao.AtualizarUsuarioAsync(Admin, inativo.Id,
                new AtualizarUsuarioRequisicao("Op", Perfis.Operador, true, [cenario.Patio.Id])));
        }

        [Fact]
        public async Task Recursos_fora_do_plano_sao_barrados()
        {
            using var cenario = new Cenario();
            await AssinarAsync(cenario, Basico() with { MaximoUsuarios = null });

            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Relatorios.ObterDaEmpresaAsync(null, null, null));
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Mensalistas.CriarAsync(Admin,
                new SalvarMensalistaRequisicao("João", null, null, null, ["ABC1234"])));
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Entrada.RegistrarPorImagemAsync(new MemoryStream([1]), "foto.jpg"));
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Impressao.AtualizarConfiguracaoAsync(Admin,
                new ConfiguracaoImpressaoDto("rede", "192.168.0.50", 9100, 80, true)));
            Assert.Empty(await cenario.Planos.RecursosDaMinhaEmpresaAsync());
        }

        [Fact]
        public async Task Mensalista_sem_app_cadastra_sem_email_mas_nao_convida()
        {
            using var cenario = new Cenario();
            await AssinarAsync(cenario, Basico(RecursosPlano.Mensalistas));

            var joao = await cenario.Mensalistas.CriarAsync(Admin, new SalvarMensalistaRequisicao("João", null, null, null, ["ABC1234"]));
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Mensalistas.AtualizarAsync(Admin, joao.Id,
                new SalvarMensalistaRequisicao("João", null, null, null, ["ABC1234"], Email: "joao@email.com")));
            Assert.Empty(cenario.Db.EmailsPendentes);
        }

        [Fact]
        public async Task Admin_ve_plano_uso_e_recursos()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("admin@empresa.com", "x", Perfis.Admin);
            await AssinarAsync(cenario, Basico(RecursosPlano.Relatorios));

            var minha = await cenario.Planos.ObterMinhaAsync();

            Assert.Equal(("Básico", 99m, 99m), (minha.NomePlano, minha.PrecoPorPatio, minha.ValorMensal));
            Assert.Equal((1, (int?)1), (minha.Patios.Usado, minha.Patios.Maximo));
            Assert.Equal((1, (int?)2), (minha.Usuarios.Usado, minha.Usuarios.Maximo));
            Assert.Equal((30, (int?)40), (minha.VagasNoMaiorPatio.Usado, minha.VagasNoMaiorPatio.Maximo));
            Assert.Equal([RecursosPlano.Relatorios], minha.Recursos.Where(r => r.Liberado).Select(r => r.Codigo));
        }

        [Fact]
        public async Task Nova_empresa_entra_no_plano_escolhido_e_respeita_as_vagas()
        {
            using var cenario = new Cenario();
            var basico = await cenario.Planos.CriarAsync(Basico());

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Plataforma.CriarEmpresaAsync(SuperAdmin,
                new CriarEmpresaRequisicao("Silva", null, "Centro", 50, "Maria", "maria@silva.com", basico.Id)));
            var empresa = await cenario.Plataforma.CriarEmpresaAsync(SuperAdmin,
                new CriarEmpresaRequisicao("Silva", null, "Centro", 40, "Maria", "maria@silva.com", basico.Id));

            Assert.Equal("Básico", empresa.NomePlano);
            Assert.Equal("Básico", (await cenario.Plataforma.ListarEmpresasAsync()).Single(e => e.Id == empresa.Id).NomePlano);
            Assert.Equal(1, (await cenario.Planos.ListarAsync()).Single(p => p.Id == basico.Id).Empresas);
        }
    }
}
