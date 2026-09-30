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
        public async Task Registro_cria_motorista_e_recusa_email_duplicado()
        {
            using var cenario = new Cenario();

            var token = await cenario.Autenticacao.RegistrarMotoristaAsync(new RegistroRequisicao("novo@example.com", "Senha@123", "Novo Motorista"));

            Assert.Equal(Perfis.Motorista, token.Perfil);
            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Autenticacao.RegistrarMotoristaAsync(new RegistroRequisicao("NOVO@example.com", "Senha@123", "Outro")));
        }

        [Fact]
        public async Task Registro_exige_senha_com_8_caracteres()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() =>
                cenario.Autenticacao.RegistrarMotoristaAsync(new RegistroRequisicao("a@b.com", "123", "Nome")));
        }
    }
}
