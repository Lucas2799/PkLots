using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;
using PkLots.Infrastructure.Data;

namespace PkLots.Application.Tests
{
    public class LoginSeguroTests
    {
        private static LoginRequisicao Login(string senha) => new("op@pklots.local", senha);

        [Fact]
        public async Task Bloqueia_a_conta_apos_cinco_senhas_erradas_por_quinze_minutos()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@pklots.local", "Correta123", Perfis.Operador);

            for (var i = 0; i < 5; i++)
                await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(Login("errada1")));

            // Mesmo com a senha certa, fica bloqueada
            var erro = await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(Login("Correta123")));
            Assert.Contains("Tente novamente em 15 minuto", erro.Message);

            cenario.Relogio.Advance(TimeSpan.FromMinutes(15));
            await cenario.Autenticacao.LoginAsync(Login("Correta123"));
        }

        [Fact]
        public async Task Login_certo_zera_as_tentativas_erradas()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@pklots.local", "Correta123", Perfis.Operador);

            for (var i = 0; i < 4; i++)
                await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(Login("errada1")));
            await cenario.Autenticacao.LoginAsync(Login("Correta123"));
            await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(Login("errada1")));

            Assert.Equal(1, (await cenario.Db.Usuarios.SingleAsync(u => u.Email == "op@pklots.local")).FalhasLogin);
        }

        [Fact]
        public async Task Email_inexistente_e_senha_errada_tem_a_mesma_mensagem()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@pklots.local", "Correta123", Perfis.Operador);

            var semConta = await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(new("ninguem@x.com", "x")));
            var senhaErrada = await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(Login("errada1")));
            Assert.Equal(semConta.Message, senhaErrada.Message);
        }
    }

    public class TrocaDeSenhaTests
    {
        [Fact]
        public async Task Troca_a_senha_e_libera_o_acesso()
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("op@pklots.local", "Temporaria1", Perfis.Operador);
            usuario.DeveTrocarSenha = true;
            await cenario.Db.SaveChangesAsync();

            Assert.True((await cenario.Autenticacao.LoginAsync(new("op@pklots.local", "Temporaria1"))).DeveTrocarSenha);

            var token = await cenario.Autenticacao.TrocarSenhaAsync(usuario.Id, new TrocarSenhaRequisicao("Temporaria1", "MinhaNova2026"));

            Assert.False(token.DeveTrocarSenha);
            Assert.False((await cenario.Db.Usuarios.SingleAsync(u => u.Email == "op@pklots.local")).DeveTrocarSenha);
            await cenario.Autenticacao.LoginAsync(new("op@pklots.local", "MinhaNova2026"));
        }

        [Theory]
        [InlineData("errada", "MinhaNova2026")]   // senha atual não confere
        [InlineData("Temporaria1", "curta1")]     // menos de 8
        [InlineData("Temporaria1", "semnumeros")] // sem números
        [InlineData("Temporaria1", "12345678")]   // sem letras
        [InlineData("Temporaria1", "Admin@123")]  // senha pública do projeto
        [InlineData("Temporaria1", "Temporaria1")] // igual à atual
        public async Task Recusa_troca_invalida(string atual, string nova)
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("op@pklots.local", "Temporaria1", Perfis.Operador);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Autenticacao.TrocarSenhaAsync(usuario.Id, new(atual, nova)));
        }

        [Fact]
        public async Task Admin_envia_link_de_nova_senha_sem_conhecer_a_senha()
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("op@pklots.local", "Antiga@123", Perfis.Operador);
            usuario.BloqueadoAte = DateTime.UtcNow.AddHours(1);
            await cenario.Db.SaveChangesAsync();

            await cenario.Administracao.EnviarLinkNovaSenhaAsync(Guid.NewGuid(), usuario.Id);
            await cenario.Autenticacao.RedefinirSenhaAsync(
                new RedefinirSenhaPorEmailRequisicao(cenario.TokenDoUltimoEmail("op@pklots.local", "redefinir"), "Nova@12345"));

            Assert.Null(usuario.BloqueadoAte);
            await cenario.Autenticacao.LoginAsync(new LoginRequisicao("op@pklots.local", "Nova@12345"));
            Assert.Contains(cenario.Db.LogsAuditoria, l => l.Acao == AcoesAuditoria.SenhaRedefinida);
        }
    }

    public class SuperUsuarioTests
    {
        [Fact]
        public async Task Comando_cria_superusuario_com_senha_temporaria_forte()
        {
            using var cenario = new Cenario();

            var (senha, criado) = await SemeadorDados.CriarOuRedefinirSuperAdminAsync(cenario.Db, cenario.HashSenha, "Dono@PkLots.com");

            Assert.True(criado);
            Assert.Equal(16, senha.Length);
            RegrasUsuario.ValidarSenha(senha); // atende à política
            var usuario = await cenario.Db.Usuarios.SingleAsync(u => u.Perfil == Perfis.SuperAdmin);
            Assert.Equal(("dono@pklots.com", true, null), (usuario.Email, usuario.DeveTrocarSenha, usuario.EmpresaId));
            Assert.True(cenario.HashSenha.Verificar(senha, usuario.SenhaHash));
        }

        [Fact]
        public async Task Comando_redefine_senha_de_superusuario_existente_e_nao_promove_outros_perfis()
        {
            using var cenario = new Cenario();
            var (primeira, _) = await SemeadorDados.CriarOuRedefinirSuperAdminAsync(cenario.Db, cenario.HashSenha, "dono@pklots.com");
            var (segunda, criado) = await SemeadorDados.CriarOuRedefinirSuperAdminAsync(cenario.Db, cenario.HashSenha, "dono@pklots.com");

            Assert.False(criado);
            Assert.NotEqual(primeira, segunda);
            Assert.Single(await cenario.Db.Usuarios.Where(u => u.Perfil == Perfis.SuperAdmin).ToListAsync());

            cenario.CriarUsuario("admin@empresa.com", "Senha2026x", Perfis.Admin);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SemeadorDados.CriarOuRedefinirSuperAdminAsync(cenario.Db, cenario.HashSenha, "admin@empresa.com"));
        }

        [Fact]
        public void Senhas_temporarias_sao_aleatorias()
        {
            var senhas = Enumerable.Range(0, 200).Select(_ => SemeadorDados.GerarSenhaTemporaria()).ToList();
            Assert.Equal(200, senhas.Distinct().Count());
        }

        [Fact]
        public async Task Inicializacao_marca_contas_com_senha_publica_e_nao_cria_demonstracao_em_producao()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("admin@pklots.local", "Admin@123", Perfis.Admin);       // senha pública
            cenario.CriarUsuario("operator@pklots.local", "Trocada2026", Perfis.Operador); // já trocou
            var empresasAntes = await cenario.Db.Empresas.CountAsync();

            await SemeadorDados.SemearAsync(cenario.Db, cenario.HashSenha, new OpcoesPlataforma { CriarDadosDemonstracao = false });

            Assert.True((await cenario.Db.Usuarios.SingleAsync(u => u.Email == "admin@pklots.local")).DeveTrocarSenha);
            Assert.False((await cenario.Db.Usuarios.SingleAsync(u => u.Email == "operator@pklots.local")).DeveTrocarSenha);
            Assert.Equal(empresasAntes, await cenario.Db.Empresas.CountAsync());
            Assert.False(await cenario.Db.Usuarios.AnyAsync(u => u.Perfil == Perfis.SuperAdmin)); // não cria superusuário sozinho
        }
    }
}
