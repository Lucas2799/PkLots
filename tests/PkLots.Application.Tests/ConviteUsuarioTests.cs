using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class ConviteUsuarioTests
    {
        private static readonly Guid Admin = Guid.NewGuid();
        private const string Email = "op@pklots.local";

        private static Task<UsuarioDto> CriarOperadorAsync(Cenario cenario) =>
            cenario.Administracao.CriarUsuarioAsync(Admin, new CriarUsuarioRequisicao(Email, "Bruno Lima", Perfis.Operador, [cenario.Patio.Id]));

        [Fact]
        public async Task Email_do_convite_traz_empresa_perfil_patios_e_endereco()
        {
            using var cenario = new Cenario();

            await CriarOperadorAsync(cenario);

            var email = await cenario.Db.EmailsPendentes.SingleAsync();
            Assert.Equal(Email, email.Para);
            Assert.Contains("Empresa Teste", email.Assunto);
            Assert.Contains("Operador(a)", email.CorpoTexto);
            Assert.Contains("Pátio Teste", email.CorpoTexto);
            Assert.Contains("https://pklots.teste/?ativar=", email.CorpoTexto);
            Assert.Contains("entre sempre pelo endereço https://pklots.teste", email.CorpoTexto);
        }

        [Fact]
        public async Task Conta_pendente_nao_entra_nem_com_senha_qualquer()
        {
            using var cenario = new Cenario();
            await CriarOperadorAsync(cenario);
            var usuario = await cenario.Db.Usuarios.SingleAsync(u => u.Email == Email);

            Assert.True(usuario.AguardandoAtivacao);
            await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() =>
                cenario.Autenticacao.LoginAsync(new LoginRequisicao(Email, "Senha@123")));
        }

        [Fact]
        public async Task Convite_mostra_os_dados_e_ativar_cria_a_senha_e_entra()
        {
            using var cenario = new Cenario();
            await CriarOperadorAsync(cenario);
            var token = cenario.TokenDoUltimoEmail(Email, "ativar");

            var convite = await cenario.Autenticacao.ObterConviteUsuarioAsync(token);
            Assert.Equal(("Bruno Lima", "Empresa Teste", Perfis.Operador), (convite.NomeCompleto, convite.NomeEmpresa, convite.Perfil));
            Assert.Equal(["Pátio Teste"], convite.Patios);

            var sessao = await cenario.Autenticacao.AtivarContaAsync(new AtivarContaRequisicao(token, "Minha@2026"));

            Assert.Equal(Perfis.Operador, sessao.Perfil);
            Assert.False(sessao.DeveTrocarSenha);
            Assert.False((await cenario.Db.Usuarios.SingleAsync(u => u.Email == Email)).AguardandoAtivacao);
            await cenario.Autenticacao.LoginAsync(new LoginRequisicao(Email, "Minha@2026"));
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Autenticacao.AtivarContaAsync(new AtivarContaRequisicao(token, "Outra@2026"))); // uso único
        }

        [Fact]
        public async Task Convite_expira_e_senha_fraca_e_recusada()
        {
            using var cenario = new Cenario();
            await CriarOperadorAsync(cenario);
            var token = cenario.TokenDoUltimoEmail(Email, "ativar");

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Autenticacao.AtivarContaAsync(new AtivarContaRequisicao(token, "123")));

            cenario.Relogio.Advance(TiposToken.ValidadeConvite + TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Autenticacao.AtivarContaAsync(new AtivarContaRequisicao(token, "Minha@2026")));
        }

        [Fact]
        public async Task Usuario_desativado_nao_ativa_a_conta()
        {
            using var cenario = new Cenario();
            var criado = await CriarOperadorAsync(cenario);
            var token = cenario.TokenDoUltimoEmail(Email, "ativar");
            await cenario.Administracao.AtualizarUsuarioAsync(Admin, criado.Id,
                new AtualizarUsuarioRequisicao("Bruno Lima", Perfis.Operador, false, [cenario.Patio.Id]));

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Autenticacao.AtivarContaAsync(new AtivarContaRequisicao(token, "Minha@2026")));
        }

        [Fact]
        public async Task Reenviar_invalida_o_convite_anterior_mesmo_com_o_filtro_da_empresa()
        {
            using var cenario = new Cenario();
            var criado = await CriarOperadorAsync(cenario);
            var primeiro = cenario.TokenDoUltimoEmail(Email, "ativar");

            // Como numa requisição do admin: o DbContext filtra pela empresa do usuário logado
            using var dbEmpresa = cenario.DbDaEmpresa(cenario.Empresa.Id);
            var contexto = new ContextoPatioTeste(dbEmpresa, cenario.Empresa.Id, Admin, Perfis.Admin, null);
            cenario.Relogio.Advance(TimeSpan.FromMinutes(1));
            await new ServicoAdministracao(dbEmpresa, contexto, cenario.Cache, cenario.HashSenha, cenario.Links, cenario.Relogio)
                .ReenviarConviteAsync(Admin, criado.Id);

            var segundo = cenario.TokenDoUltimoEmail(Email, "ativar");
            Assert.NotEqual(primeiro, segundo);
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.Autenticacao.ObterConviteUsuarioAsync(primeiro));
            await cenario.Autenticacao.ObterConviteUsuarioAsync(segundo);
        }

        [Fact]
        public async Task Reenviar_e_link_de_senha_respeitam_a_situacao_da_conta()
        {
            using var cenario = new Cenario();
            var criado = await CriarOperadorAsync(cenario);

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Administracao.EnviarLinkNovaSenhaAsync(Admin, criado.Id));

            await cenario.Autenticacao.AtivarContaAsync(new AtivarContaRequisicao(cenario.TokenDoUltimoEmail(Email, "ativar"), "Minha@2026"));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Administracao.ReenviarConviteAsync(Admin, criado.Id));
        }

        [Fact]
        public async Task Esqueci_senha_de_conta_pendente_reenvia_o_convite()
        {
            using var cenario = new Cenario();
            await CriarOperadorAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromMinutes(1));

            await cenario.Autenticacao.SolicitarRedefinicaoAsync(new EsqueciSenhaRequisicao(Email));

            var ultimo = cenario.Db.EmailsPendentes.OrderBy(e => e.CriadoEm).AsEnumerable().Last();
            Assert.Contains("?ativar=", ultimo.CorpoTexto);
            Assert.DoesNotContain(cenario.Db.TokensUsuario, t => t.Tipo == TiposToken.RedefinirSenha);
        }

        [Fact]
        public async Task Superadmin_reenvia_convite_do_admin_da_empresa()
        {
            using var cenario = new Cenario();
            var empresa = await cenario.Plataforma.CriarEmpresaAsync(Guid.NewGuid(),
                new CriarEmpresaRequisicao("Estacionamentos Silva", null, "Pátio Centro", 20, "Maria Silva", "maria@silva.com"));
            cenario.Relogio.Advance(TimeSpan.FromMinutes(1));

            var atualizada = await cenario.Plataforma.ReenviarConvitesAsync(Guid.NewGuid(), empresa.Id);

            Assert.Equal(1, atualizada.ConvitesPendentes);
            Assert.Equal(2, await cenario.Db.EmailsPendentes.CountAsync(e => e.Para == "maria@silva.com"));
        }
    }
}
