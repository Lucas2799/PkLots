using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Tests
{
    public class AcessoMensalistaTests
    {
        private const string EmailCliente = "cliente@teste.com";

        private static SalvarMensalistaRequisicao Requisicao(string placa = "ABC1D23", string? email = EmailCliente, bool ativo = true) =>
            new("Ana Souza", null, null, null, [placa], ativo, email);

        // Mensalista cadastrado com e-mail e convite aceito (conta nova); devolve a conta do app
        public static async Task<Usuario> MensalistaComAcessoAsync(Cenario cenario, string placa = "ABC1D23", string email = EmailCliente)
        {
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao(placa, email));
            await cenario.AcessoMensalista.AceitarConviteAsync(cenario.TokenDoUltimoEmail(email, "convite"),
                new AceitarConviteRequisicao(null, "Senha@1234"));
            return await cenario.Db.Usuarios.SingleAsync(u => u.Email == email);
        }

        [Fact]
        public async Task Cadastro_com_email_envia_convite_e_fica_pendente()
        {
            using var cenario = new Cenario();

            var mensalista = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao(email: " Cliente@Teste.com "));

            Assert.Equal(EmailCliente, mensalista.Email);
            Assert.Equal(AcessoAppMensalista.ConvitePendente, mensalista.AcessoApp);
            var email = await cenario.Db.EmailsPendentes.SingleAsync();
            Assert.Equal(EmailCliente, email.Para);
            Assert.Contains("Pátio Teste", email.Assunto);
            Assert.Contains("https://pklots.teste/?convite=", email.CorpoTexto);
            Assert.Contains(cenario.Db.LogsAuditoria, l => l.Acao == AcoesAuditoria.ConviteMensalistaEnviado);
        }

        [Fact]
        public async Task Cadastro_sem_email_nao_envia_nada()
        {
            using var cenario = new Cenario();

            var mensalista = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao(email: null));

            Assert.Equal(AcessoAppMensalista.SemEmail, mensalista.AcessoApp);
            Assert.Empty(cenario.Db.EmailsPendentes);
        }

        [Fact]
        public async Task Convite_mostra_placas_mascaradas()
        {
            using var cenario = new Cenario();
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao());

            var convite = await cenario.AcessoMensalista.ObterConviteAsync(cenario.TokenDoUltimoEmail(EmailCliente, "convite"));

            Assert.Equal("Pátio Teste", convite.NomePatio);
            Assert.Equal(["ABC-**23"], convite.PlacasMascaradas);
            Assert.False(convite.ContaExistente);
        }

        [Fact]
        public async Task Aceitar_cria_conta_vinculada_e_ja_entra()
        {
            using var cenario = new Cenario();
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao());
            var token = cenario.TokenDoUltimoEmail(EmailCliente, "convite");

            var aceite = await cenario.AcessoMensalista.AceitarConviteAsync(token, new AceitarConviteRequisicao(null, "Senha@1234"));

            Assert.True(aceite.ContaCriada);
            Assert.Equal(Perfis.Motorista, aceite.Token!.Perfil);
            var usuario = await cenario.Db.Usuarios.SingleAsync(u => u.Email == EmailCliente);
            Assert.Equal("Ana Souza", usuario.NomeCompleto);
            Assert.Null(usuario.EmpresaId);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();
            Assert.Equal(usuario.Id, mensalista.UsuarioId);
            Assert.Equal(cenario.Relogio.GetUtcNow().UtcDateTime, mensalista.VinculadoEm);
            Assert.Equal(AcessoAppMensalista.Ativo, (await cenario.Mensalistas.ObterAsync(mensalista.Id)).AcessoApp);

            // Uso único
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.AcessoMensalista.AceitarConviteAsync(token, new AceitarConviteRequisicao(null, "Senha@1234")));
        }

        [Fact]
        public async Task Conta_existente_de_mensalista_so_e_vinculada()
        {
            using var cenario = new Cenario();
            var conta = cenario.CriarUsuario(EmailCliente, "Antiga@123");
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao());
            var token = cenario.TokenDoUltimoEmail(EmailCliente, "convite");
            Assert.True((await cenario.AcessoMensalista.ObterConviteAsync(token)).ContaExistente);

            var aceite = await cenario.AcessoMensalista.AceitarConviteAsync(token, new AceitarConviteRequisicao(null, null));

            Assert.False(aceite.ContaCriada);
            Assert.Null(aceite.Token);
            Assert.True(cenario.HashSenha.Verificar("Antiga@123", conta.SenhaHash)); // senha não muda
            Assert.Equal(conta.Id, (await cenario.Db.Mensalistas.SingleAsync()).UsuarioId);
        }

        [Fact]
        public async Task Email_de_conta_de_gestao_nao_vira_mensalista()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@teste.com", "Senha@123", Perfis.Operador);
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao(email: "op@teste.com"));

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.AcessoMensalista.AceitarConviteAsync(
                cenario.TokenDoUltimoEmail("op@teste.com", "convite"), new AceitarConviteRequisicao(null, null)));
            Assert.Null((await cenario.Db.Mensalistas.SingleAsync()).UsuarioId);
        }

        [Fact]
        public async Task Convite_expira()
        {
            using var cenario = new Cenario();
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao());
            var token = cenario.TokenDoUltimoEmail(EmailCliente, "convite");

            cenario.Relogio.Advance(TiposToken.ValidadeConvite + TimeSpan.FromSeconds(1));

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.AcessoMensalista.ObterConviteAsync(token));
        }

        [Fact]
        public async Task Trocar_o_email_revoga_o_acesso_e_invalida_o_convite_anterior()
        {
            using var cenario = new Cenario();
            var conta = await MensalistaComAcessoAsync(cenario);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();

            cenario.Relogio.Advance(TimeSpan.FromMinutes(1));
            await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, mensalista.Id, Requisicao(email: "novo@teste.com"));

            Assert.Null(mensalista.UsuarioId);
            Assert.Empty(await cenario.Motorista.ListarContratosAsync(conta.Id));
            Assert.Contains(cenario.Db.LogsAuditoria, l => l.Acao == AcoesAuditoria.AcessoMensalistaRevogado);
            Assert.Contains(cenario.Db.EmailsPendentes, e => e.Para == "novo@teste.com");
        }

        [Fact]
        public async Task Salvar_com_o_mesmo_email_nao_reenvia_nem_desvincula()
        {
            using var cenario = new Cenario();
            var conta = await MensalistaComAcessoAsync(cenario);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();

            await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, mensalista.Id, Requisicao(email: "CLIENTE@teste.com"));

            Assert.Equal(conta.Id, mensalista.UsuarioId);
            Assert.Single(cenario.Db.EmailsPendentes);
        }

        [Fact]
        public async Task Remover_o_email_revoga_o_acesso()
        {
            using var cenario = new Cenario();
            var conta = await MensalistaComAcessoAsync(cenario);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();

            var dto = await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, mensalista.Id, Requisicao(email: ""));

            Assert.Equal(AcessoAppMensalista.SemEmail, dto.AcessoApp);
            Assert.Empty(await cenario.Motorista.ListarContratosAsync(conta.Id));
        }

        [Fact]
        public async Task Mensalista_desativado_some_do_app_e_nao_aceita_convite()
        {
            using var cenario = new Cenario();
            var conta = await MensalistaComAcessoAsync(cenario);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();

            await cenario.Mensalistas.AtualizarAsync(cenario.UsuarioAtualId, mensalista.Id, Requisicao(ativo: false));

            Assert.Empty(await cenario.Motorista.ListarContratosAsync(conta.Id));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Mensalistas.ReenviarConviteAsync(cenario.UsuarioAtualId, mensalista.Id));
        }

        [Fact]
        public async Task Reenviar_convite_invalida_o_link_anterior()
        {
            using var cenario = new Cenario();
            var mensalista = await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao());
            var primeiro = cenario.TokenDoUltimoEmail(EmailCliente, "convite");

            cenario.Relogio.Advance(TimeSpan.FromMinutes(1));
            await cenario.Mensalistas.ReenviarConviteAsync(cenario.UsuarioAtualId, mensalista.Id);
            var segundo = cenario.TokenDoUltimoEmail(EmailCliente, "convite");

            Assert.NotEqual(primeiro, segundo);
            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() => cenario.AcessoMensalista.ObterConviteAsync(primeiro));
            await cenario.AcessoMensalista.ObterConviteAsync(segundo);
        }

        [Fact]
        public async Task Reenviar_recusa_quem_ja_ativou()
        {
            using var cenario = new Cenario();
            await MensalistaComAcessoAsync(cenario);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Mensalistas.ReenviarConviteAsync(cenario.UsuarioAtualId, mensalista.Id));
        }

        [Fact]
        public async Task App_mostra_o_contrato_e_o_veiculo_estacionado_sem_cobranca()
        {
            using var cenario = new Cenario();
            var conta = await MensalistaComAcessoAsync(cenario);
            var mensalista = await cenario.Db.Mensalistas.SingleAsync();
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            await cenario.Mensalistas.RegistrarPagamentoAsync(cenario.UsuarioAtualId, mensalista.Id,
                new RegistrarPagamentoRequisicao(1, 200m, FormasPagamento.Dinheiro, null));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1D23"));
            cenario.Relogio.Advance(TimeSpan.FromHours(2));

            var contrato = Assert.Single(await cenario.Motorista.ListarContratosAsync(conta.Id));

            Assert.Equal(["ABC1D23"], contrato.Placas);
            Assert.Equal(SituacaoMensalista.EmDia, contrato.Situacao);
            var estacionado = Assert.Single(contrato.Estacionados);
            Assert.Equal("2h 0m", estacionado.Duracao);
            Assert.Equal(0m, estacionado.ValorEstimado);
        }

        [Fact]
        public async Task Historico_comeca_no_vinculo()
        {
            using var cenario = new Cenario();
            // Estadia antes do contrato ter acesso ao app
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1D23"));
            cenario.Relogio.Advance(TimeSpan.FromHours(1));
            await cenario.SairAsync("ABC1D23");

            cenario.Relogio.Advance(TimeSpan.FromDays(1));
            var conta = await MensalistaComAcessoAsync(cenario);
            cenario.Relogio.Advance(TimeSpan.FromMinutes(5));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1D23"));

            var historico = await cenario.Motorista.ObterHistoricoAsync(conta.Id);

            var item = Assert.Single(historico);
            Assert.Equal(SituacaoEntrada.Ativa, item.Situacao);
        }

        [Fact]
        public async Task Conta_de_mensalista_em_dois_patios_ve_cada_contrato_separado()
        {
            using var cenario = new Cenario();
            var conta = await MensalistaComAcessoAsync(cenario, "ABC1D23");

            // Outro pátio da mesma empresa, outro contrato para o mesmo e-mail
            var segundo = ServicoPatios.NovoPatio(cenario.Empresa.Id, "Pátio Centro", 10, Cenario.Inicio.UtcDateTime);
            cenario.Db.Patios.Add(segundo);
            await cenario.Db.SaveChangesAsync();
            cenario.PatioEscolhidoId = segundo.Id;
            cenario.Relogio.Advance(TimeSpan.FromMinutes(1));
            await cenario.Mensalistas.CriarAsync(cenario.UsuarioAtualId, Requisicao("XYZ9K88"));
            await cenario.AcessoMensalista.AceitarConviteAsync(cenario.TokenDoUltimoEmail(EmailCliente, "convite"),
                new AceitarConviteRequisicao(null, null));

            var contratos = await cenario.Motorista.ListarContratosAsync(conta.Id);

            Assert.Equal(["Pátio Centro", "Pátio Teste"], contratos.Select(c => c.NomePatio));
            Assert.Equal(["XYZ9K88"], contratos[0].Placas);
            Assert.Equal(["ABC1D23"], contratos[1].Placas);
        }

        [Theory]
        [InlineData("ABC1D23", "ABC-**23")]
        [InlineData("ABC1234", "ABC-**34")]
        public void Mascara_placa(string placa, string esperado) => Assert.Equal(esperado, ServicoAcessoMensalista.MascararPlaca(placa));
    }
}
