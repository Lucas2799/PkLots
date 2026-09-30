using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Tests
{
    public class ServicoAdministracaoTests
    {
        private static readonly Guid AdminQualquer = Guid.NewGuid();

        // ---------- Configuração ----------

        [Fact]
        public async Task Atualiza_preco_tolerancia_e_nome_com_auditoria()
        {
            using var cenario = new Cenario();

            var config = await cenario.Administracao.AtualizarConfiguracaoAsync(AdminQualquer,
                new AtualizarConfiguracaoRequisicao("Estacionamento Centro", 12.50m, 15));

            Assert.Equal("Estacionamento Centro", config.Nome);
            Assert.Equal(12.50m, config.ValorHora);
            Assert.Equal(15, config.MinutosTolerancia);
            Assert.Equal(30, config.TotalVagas);
            var log = await cenario.Db.LogsAuditoria.SingleAsync();
            Assert.Equal(AcoesAuditoria.ConfiguracaoAlterada, log.Acao);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(-5, 0)]
        [InlineData(1001, 0)]
        [InlineData(10.555, 0)]
        [InlineData(15, -1)]
        [InlineData(15, 241)]
        public async Task Recusa_configuracao_invalida(double valorHora, int tolerancia)
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.AtualizarConfiguracaoAsync(AdminQualquer,
                new AtualizarConfiguracaoRequisicao("Pátio", (decimal)valorHora, tolerancia)));
        }

        [Fact]
        public async Task Saida_dentro_da_tolerancia_fica_isenta()
        {
            using var cenario = new Cenario();
            await cenario.Administracao.AtualizarConfiguracaoAsync(AdminQualquer, new AtualizarConfiguracaoRequisicao("Pátio", 15m, 15));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromMinutes(10));

            var saida = await cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234"));

            Assert.Equal(0m, saida.ValorTotal);
            Assert.Equal(SituacaoPagamento.Isento, saida.SituacaoPagamento);
        }

        [Fact]
        public async Task Novo_valor_hora_vale_para_as_proximas_saidas()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            cenario.Relogio.Advance(TimeSpan.FromHours(2));
            await cenario.Administracao.AtualizarConfiguracaoAsync(AdminQualquer, new AtualizarConfiguracaoRequisicao("Pátio", 10m, 0));

            var saida = await cenario.Saida.RegistrarAsync(new RegistrarSaidaRequisicao("ABC1234"));

            Assert.Equal(20.00m, saida.ValorTotal);
        }

        // ---------- Quantidade de vagas ----------

        [Fact]
        public async Task Aumentar_cria_vagas_seguindo_as_fileiras()
        {
            using var cenario = new Cenario();

            var config = await cenario.Administracao.AlterarQuantidadeVagasAsync(AdminQualquer, 35);

            Assert.Equal(35, config.TotalVagas);
            var numeros = (await cenario.Vagas.ListarAsync()).Select(v => v.Numero).ToList();
            Assert.Equal(35, numeros.Count);
            Assert.Equal(["D1", "D2", "D3", "D4", "D5"], numeros[^5..]);
            Assert.Equal(35, (await cenario.Vagas.ObterResumoAsync()).Total);
        }

        [Fact]
        public async Task Diminuir_desativa_as_ultimas_vagas_sem_apagar()
        {
            using var cenario = new Cenario();

            await cenario.Administracao.AlterarQuantidadeVagasAsync(AdminQualquer, 25);

            var ativas = await cenario.Vagas.ListarAsync();
            Assert.Equal(25, ativas.Count);
            Assert.Equal("C5", ativas[^1].Numero);
            Assert.Equal(30, await cenario.Db.Vagas.CountAsync()); // continuam no banco (histórico)
        }

        [Fact]
        public async Task Nao_remove_vaga_ocupada()
        {
            using var cenario = new Cenario(vagasPorFileira: 1); // A1, B1, C1
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222")); // ocupa B1

            // Layout padrão com 1 vaga mantém só "A1": B1 ocupada impede
            var erro = await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Administracao.AlterarQuantidadeVagasAsync(AdminQualquer, 1));

            Assert.Contains("B1", erro.Message);
        }

        [Fact]
        public async Task Entrada_nao_usa_vaga_desativada_e_reativacao_devolve_a_vaga()
        {
            using var cenario = new Cenario();
            await cenario.Administracao.AlterarQuantidadeVagasAsync(AdminQualquer, 1);

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222")));

            await cenario.Administracao.AlterarQuantidadeVagasAsync(AdminQualquer, 2);
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));

            Assert.Equal("A2", entrada.NumeroVaga);
            Assert.Equal(30, await cenario.Db.Vagas.CountAsync()); // reativou em vez de criar
        }

        [Theory]
        [InlineData(0)]
        [InlineData(261)]
        public async Task Recusa_quantidade_fora_do_limite(int total)
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.AlterarQuantidadeVagasAsync(AdminQualquer, total));
        }

        // ---------- Usuários ----------

        [Fact]
        public async Task Cria_usuario_com_perfil_e_ele_consegue_entrar()
        {
            using var cenario = new Cenario();

            var usuario = await cenario.Administracao.CriarUsuarioAsync(AdminQualquer,
                new CriarUsuarioRequisicao("Operador2@PkLots.local", "Operador Dois", "Senha@123", "Operador"));

            Assert.Equal("operador2@pklots.local", usuario.Email);
            Assert.Equal(Perfis.Operador, usuario.Perfil);
            var token = await cenario.Autenticacao.LoginAsync(new LoginRequisicao("operador2@pklots.local", "Senha@123"));
            Assert.Equal(Perfis.Operador, token.Perfil);
        }

        [Fact]
        public async Task Recusa_email_duplicado_e_perfil_invalido()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("ja@existe.com", "Senha@123");

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Administracao.CriarUsuarioAsync(AdminQualquer,
                new CriarUsuarioRequisicao("ja@existe.com", "Nome", "Senha@123", Perfis.Motorista)));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.CriarUsuarioAsync(AdminQualquer,
                new CriarUsuarioRequisicao("novo@existe.com", "Nome", "Senha@123", "gerente")));
        }

        [Fact]
        public async Task Admin_nao_remove_o_proprio_acesso()
        {
            using var cenario = new Cenario();
            var admin = cenario.CriarUsuario("admin@pklots.local", "Admin@123", Perfis.Admin);
            cenario.CriarUsuario("admin2@pklots.local", "Admin@123", Perfis.Admin);

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.AtualizarUsuarioAsync(admin.Id, admin.Id,
                new AtualizarUsuarioRequisicao("Admin", Perfis.Operador, true)));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.AtualizarUsuarioAsync(admin.Id, admin.Id,
                new AtualizarUsuarioRequisicao("Admin", Perfis.Admin, false)));
        }

        [Fact]
        public async Task Mantem_pelo_menos_um_admin_ativo()
        {
            using var cenario = new Cenario();
            var unicoAdmin = cenario.CriarUsuario("admin@pklots.local", "Admin@123", Perfis.Admin);

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Administracao.AtualizarUsuarioAsync(AdminQualquer, unicoAdmin.Id,
                new AtualizarUsuarioRequisicao("Admin", Perfis.Motorista, true)));
        }

        [Fact]
        public async Task Desativa_usuario_e_ele_nao_entra_mais()
        {
            using var cenario = new Cenario();
            var admin = cenario.CriarUsuario("admin@pklots.local", "Admin@123", Perfis.Admin);
            var operador = cenario.CriarUsuario("op@pklots.local", "Senha@123", Perfis.Operador);

            var alterado = await cenario.Administracao.AtualizarUsuarioAsync(admin.Id, operador.Id,
                new AtualizarUsuarioRequisicao("Operador", Perfis.Operador, false));

            Assert.False(alterado.Ativo);
            await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() =>
                cenario.Autenticacao.LoginAsync(new LoginRequisicao("op@pklots.local", "Senha@123")));
        }

        [Fact]
        public async Task Redefine_senha()
        {
            using var cenario = new Cenario();
            var usuario = cenario.CriarUsuario("driver@example.com", "Antiga@123");

            await cenario.Administracao.RedefinirSenhaAsync(AdminQualquer, usuario.Id, "Nova@1234");

            await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() =>
                cenario.Autenticacao.LoginAsync(new LoginRequisicao("driver@example.com", "Antiga@123")));
            await cenario.Autenticacao.LoginAsync(new LoginRequisicao("driver@example.com", "Nova@1234"));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Administracao.RedefinirSenhaAsync(AdminQualquer, usuario.Id, "curta"));
        }
    }
}
