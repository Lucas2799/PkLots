using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class ServicoAutenticacaoTests
    {
        [Fact]
        public async Task Login_valido_retorna_token_e_atualiza_ultimo_login()
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("admin@pklots.local", "Admin@123", Perfis.Admin);

            var token = await cenario.Autenticacao.LoginAsync(new LoginRequisicao(" ADMIN@pklots.local ", "Admin@123"));

            Assert.Equal(Perfis.Admin, token.Perfil);
            Assert.Equal(86400, token.ExpiraEm);
            Assert.Equal(Cenario.Inicio.UtcDateTime, (await cenario.Db.Usuarios.SingleAsync()).UltimoLogin);
        }

        [Theory]
        [InlineData("admin@pklots.local", "errada")]
        [InlineData("naoexiste@pklots.local", "Admin@123")]
        public async Task Login_invalido_e_recusado(string email, string senha)
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("admin@pklots.local", "Admin@123", Perfis.Admin);

            await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() => cenario.Autenticacao.LoginAsync(new LoginRequisicao(email, senha)));
        }

        [Fact]
        public async Task Usuario_inativo_nao_faz_login()
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("driver@example.com", "Driver@123");
            usuario.Ativo = false;
            await cenario.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() =>
                cenario.Autenticacao.LoginAsync(new LoginRequisicao("driver@example.com", "Driver@123")));
        }

        [Fact]
        public async Task Esqueci_senha_envia_link_e_redefine_desbloqueando_a_conta()
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("op@teste.com", "Antiga@123", Perfis.Operador);
            usuario.BloqueadoAte = Cenario.Inicio.UtcDateTime.AddMinutes(10);
            usuario.DeveTrocarSenha = true;
            await cenario.Db.SaveChangesAsync();

            await cenario.Autenticacao.SolicitarRedefinicaoAsync(new EsqueciSenhaRequisicao(" OP@teste.com "));
            var email = await cenario.Db.EmailsPendentes.SingleAsync();
            Assert.Equal("op@teste.com", email.Para);
            Assert.Contains("https://pklots.teste/?redefinir=", email.CorpoTexto);
            var token = cenario.TokenDoUltimoEmail("op@teste.com", "redefinir");
            Assert.DoesNotContain(cenario.Db.TokensUsuario, t => t.HashToken == token); // só o hash fica no banco

            await cenario.Autenticacao.RedefinirSenhaAsync(new RedefinirSenhaPorEmailRequisicao(token, "Nova@1234"));

            Assert.True(cenario.HashSenha.Verificar("Nova@1234", usuario.SenhaHash));
            Assert.Null(usuario.BloqueadoAte);
            Assert.False(usuario.DeveTrocarSenha);
            await cenario.Autenticacao.LoginAsync(new LoginRequisicao("op@teste.com", "Nova@1234"));

            // O link só vale uma vez
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Autenticacao.RedefinirSenhaAsync(new RedefinirSenhaPorEmailRequisicao(token, "Outra@1234")));
        }

        [Fact]
        public async Task Esqueci_senha_nao_revela_se_o_email_existe_nem_atende_superadmin()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("root@teste.com", "Senha@123", Perfis.SuperAdmin);

            await cenario.Autenticacao.SolicitarRedefinicaoAsync(new EsqueciSenhaRequisicao("ninguem@teste.com"));
            await cenario.Autenticacao.SolicitarRedefinicaoAsync(new EsqueciSenhaRequisicao("root@teste.com"));

            Assert.Empty(cenario.Db.EmailsPendentes);
            Assert.Empty(cenario.Db.TokensUsuario);
        }

        [Fact]
        public async Task Link_de_redefinicao_expira_em_uma_hora()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@teste.com", "Antiga@123", Perfis.Operador);
            await cenario.Autenticacao.SolicitarRedefinicaoAsync(new EsqueciSenhaRequisicao("op@teste.com"));
            var token = cenario.TokenDoUltimoEmail("op@teste.com", "redefinir");

            cenario.Relogio.Advance(TiposToken.ValidadeRedefinicao + TimeSpan.FromSeconds(1));

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Autenticacao.RedefinirSenhaAsync(new RedefinirSenhaPorEmailRequisicao(token, "Nova@1234")));
        }

        [Fact]
        public async Task Nova_senha_segue_a_politica()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@teste.com", "Antiga@123", Perfis.Operador);
            await cenario.Autenticacao.SolicitarRedefinicaoAsync(new EsqueciSenhaRequisicao("op@teste.com"));
            var token = cenario.TokenDoUltimoEmail("op@teste.com", "redefinir");

            await Assert.ThrowsAsync<ExcecaoNegocio>(() =>
                cenario.Autenticacao.RedefinirSenhaAsync(new RedefinirSenhaPorEmailRequisicao(token, "123")));
        }
    }
}
