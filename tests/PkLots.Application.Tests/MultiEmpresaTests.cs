using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Tests
{
    // Isolamento entre empresas: garantido pelos filtros do DbContext a partir do token
    public class IsolamentoEntreEmpresasTests
    {
        [Fact]
        public async Task Usuario_de_uma_empresa_nao_ve_dados_de_outra()
        {
            using var cenario = new Cenario();
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            var (outra, patioOutra) = cenario.CriarOutraEmpresa();
            cenario.Db.EntradasVeiculos.Add(new EntradaVeiculo { PatioId = patioOutra.Id, Placa = "ZZZ9999", DataHoraEntrada = DateTime.UtcNow });
            cenario.Db.Usuarios.Add(new Usuario { EmpresaId = outra.Id, Email = "admin@outra.com", Perfil = Perfis.Admin });
            await cenario.Db.SaveChangesAsync();

            using var db = cenario.DbDaEmpresa(cenario.Empresa.Id);

            Assert.Equal([cenario.Empresa.Id], await db.Empresas.Select(e => e.Id).ToListAsync());
            Assert.Equal([cenario.Patio.Id], await db.Patios.Select(p => p.Id).ToListAsync());
            Assert.Equal(["AAA1111"], await db.EntradasVeiculos.Select(e => e.Placa).ToListAsync());
            Assert.Equal(30, await db.Vagas.CountAsync());
            Assert.DoesNotContain(await db.Usuarios.Select(u => u.Email).ToListAsync(), e => e == "admin@outra.com");
            Assert.All(await db.LogsAuditoria.ToListAsync(), l => Assert.Equal(cenario.Patio.Id, l.PatioId));
        }

        [Fact]
        public async Task Sem_empresa_no_token_nao_ha_filtro_e_os_servicos_filtram_por_conta_propria()
        {
            using var cenario = new Cenario();
            cenario.CriarOutraEmpresa();

            // Startup/superadmin/motorista: todas as empresas visíveis no nível do banco
            Assert.Equal(2, await cenario.Db.Empresas.CountAsync());
        }

        [Fact]
        public async Task Operador_so_acessa_os_patios_vinculados()
        {
            using var cenario = new Cenario();
            var segundo = (await cenario.Patios.CriarAsync(Guid.NewGuid(), new CriarPatioRequisicao("Pátio Shopping", 10))).Id;
            var operador = cenario.CriarUsuario("op@pklots.local", "Senha@123", Perfis.Operador); // vinculado só ao primeiro
            cenario.PerfilAtual = Perfis.Operador;
            cenario.UsuarioAtualId = operador.Id;

            Assert.Equal([cenario.Patio.Id], await cenario.ContextoPatio.ObterPatiosPermitidosAsync());

            cenario.PatioEscolhidoId = segundo;
            await Assert.ThrowsAsync<ExcecaoProibido>(() => cenario.Vagas.ObterResumoAsync());
        }

        [Fact]
        public async Task Admin_acessa_todos_os_patios_e_os_dados_ficam_separados_por_patio()
        {
            using var cenario = new Cenario();
            var shopping = (await cenario.Patios.CriarAsync(Guid.NewGuid(), new CriarPatioRequisicao("Pátio Shopping", 10))).Id;

            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111")); // pátio padrão
            cenario.PatioEscolhidoId = shopping;
            var noShopping = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("BBB2222"));

            Assert.Equal("A1", noShopping.NumeroVaga); // vagas próprias do pátio
            Assert.Equal(10, (await cenario.Vagas.ObterResumoAsync()).Total);
            Assert.Equal(["BBB2222"], (await cenario.Dashboard.ObterAsync()).Estacionados.Select(e => e.Placa));
            // Mesma placa pode estar em pátios diferentes? Não: um carro está num lugar só — mas cada pátio tem suas entradas
            cenario.PatioEscolhidoId = cenario.Patio.Id;
            Assert.Equal(["AAA1111"], (await cenario.Dashboard.ObterAsync()).Estacionados.Select(e => e.Placa));
        }
    }

    public class ServicoPatiosTests
    {
        private static readonly Guid Admin = Guid.NewGuid();

        [Fact]
        public async Task Cria_patio_com_vagas_e_preco()
        {
            using var cenario = new Cenario();

            var patio = await cenario.Patios.CriarAsync(Admin, new CriarPatioRequisicao("Pátio Shopping", 25, 12.5m, 10));

            Assert.Equal(25, patio.TotalVagas);
            var salvo = await cenario.Db.Patios.SingleAsync(p => p.Id == patio.Id);
            Assert.Equal((cenario.Empresa.Id, 12.5m, 10), (salvo.EmpresaId, salvo.ValorHora, salvo.MinutosTolerancia));
            Assert.Equal("C5", (await cenario.Db.Vagas.Where(v => v.PatioId == patio.Id).OrderBy(v => v.Fileira).ThenBy(v => v.Indice).LastAsync()).Numero);
        }

        [Fact]
        public async Task Recusa_nome_repetido_na_mesma_empresa()
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Patios.CriarAsync(Admin, new CriarPatioRequisicao("pátio teste")));
        }

        [Fact]
        public async Task Nao_desativa_patio_com_carros_nem_o_ultimo_ativo()
        {
            using var cenario = new Cenario();
            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Patios.AtualizarAsync(Admin, cenario.Patio.Id, new AtualizarPatioRequisicao("Pátio Teste", false)));

            var segundo = await cenario.Patios.CriarAsync(Admin, new CriarPatioRequisicao("Pátio Shopping", 5));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));
            await Assert.ThrowsAsync<ExcecaoConflito>(() =>
                cenario.Patios.AtualizarAsync(Admin, cenario.Patio.Id, new AtualizarPatioRequisicao("Pátio Teste", false)));

            var desativado = await cenario.Patios.AtualizarAsync(Admin, segundo.Id, new AtualizarPatioRequisicao("Pátio Shopping", false));
            Assert.False(desativado.Ativo);
            Assert.DoesNotContain(segundo.Id, await cenario.ContextoPatio.ObterPatiosPermitidosAsync());
        }

        [Fact]
        public async Task Admin_nao_altera_patio_de_outra_empresa()
        {
            using var cenario = new Cenario();
            var (_, patioOutra) = cenario.CriarOutraEmpresa();

            await Assert.ThrowsAsync<ExcecaoNaoEncontrado>(() =>
                cenario.Patios.AtualizarAsync(Admin, patioOutra.Id, new AtualizarPatioRequisicao("Invadido", true)));
        }
    }

    public class ServicoPlataformaTests
    {
        private static readonly Guid SuperAdmin = Guid.NewGuid();

        private static CriarEmpresaRequisicao Requisicao(string nome = "Estacionamentos Silva", string email = "admin@silva.com") =>
            new(nome, "12.345.678/0001-90", "Pátio Centro", 40, "Maria Silva", email, "Senha@123");

        [Fact]
        public async Task Cria_empresa_com_patio_e_admin_que_consegue_entrar()
        {
            using var cenario = new Cenario();

            var empresa = await cenario.Plataforma.CriarEmpresaAsync(SuperAdmin, Requisicao());

            Assert.Equal((1, 1), (empresa.Patios, empresa.Usuarios));
            Assert.Equal(40, await cenario.Db.Vagas.CountAsync(v => v.Patio.EmpresaId == empresa.Id));
            var token = await cenario.Autenticacao.LoginAsync(new LoginRequisicao("admin@silva.com", "Senha@123"));
            Assert.Equal(Perfis.Admin, token.Perfil);
            Assert.Equal(empresa.Id, (await cenario.Db.Usuarios.SingleAsync(u => u.Email == "admin@silva.com")).EmpresaId);
        }

        [Fact]
        public async Task Recusa_empresa_ou_email_repetidos()
        {
            using var cenario = new Cenario();
            await cenario.Plataforma.CriarEmpresaAsync(SuperAdmin, Requisicao());

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Plataforma.CriarEmpresaAsync(SuperAdmin, Requisicao(email: "outro@silva.com")));
            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Plataforma.CriarEmpresaAsync(SuperAdmin, Requisicao("Outra Ltda")));
        }

        [Fact]
        public async Task Empresa_desativada_bloqueia_o_login_dos_usuarios()
        {
            using var cenario = new Cenario();
            var empresa = await cenario.Plataforma.CriarEmpresaAsync(SuperAdmin, Requisicao());

            await cenario.Plataforma.AtualizarEmpresaAsync(SuperAdmin, empresa.Id, new AtualizarEmpresaRequisicao(empresa.Nome, null, false));

            var erro = await Assert.ThrowsAsync<ExcecaoNaoAutorizado>(() =>
                cenario.Autenticacao.LoginAsync(new LoginRequisicao("admin@silva.com", "Senha@123")));
            Assert.Contains("desativado", erro.Message);
        }

        [Fact]
        public async Task Lista_todas_as_empresas()
        {
            using var cenario = new Cenario();
            await cenario.Plataforma.CriarEmpresaAsync(SuperAdmin, Requisicao());

            var empresas = await cenario.Plataforma.ListarEmpresasAsync();

            Assert.Equal(["Empresa Teste", "Estacionamentos Silva"], empresas.Select(e => e.Nome).OrderBy(n => n));
        }
    }

    public class MotoristaEmVariosPatiosTests
    {
        [Fact]
        public async Task Placa_do_motorista_vale_em_patios_de_qualquer_empresa()
        {
            using var cenario = new Cenario();
            var motorista = cenario.CriarUsuario("driver@example.com", "Senha@123");
            await cenario.Motorista.CadastrarVeiculoAsync(motorista.Id, new CadastrarVeiculoRequisicao("ABC1234", null, null));

            // Estaciona num pátio de outra empresa
            var (_, patioOutra) = cenario.CriarOutraEmpresa("Estacionamentos Silva");
            cenario.Db.EntradasVeiculos.Add(new EntradaVeiculo
            {
                PatioId = patioOutra.Id,
                Placa = "ABC1234",
                DataHoraEntrada = Cenario.Inicio.UtcDateTime,
                Situacao = SituacaoEntrada.Ativa
            });
            await cenario.Db.SaveChangesAsync();
            cenario.Relogio.Advance(TimeSpan.FromHours(1));

            var atual = await cenario.Motorista.ObterEstacionamentoAtualAsync(motorista.Id);

            Assert.NotNull(atual);
            Assert.Equal("Pátio Estacionamentos Silva", atual.NomePatio);
            Assert.Equal(15.00m, atual.ValorEstimado);
        }

        [Fact]
        public async Task Lista_patios_ativos_de_empresas_ativas_com_vagas_livres()
        {
            using var cenario = new Cenario();
            var (outra, _) = cenario.CriarOutraEmpresa("Estacionamentos Silva");
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("AAA1111"));

            var patios = await cenario.Motorista.ListarPatiosAsync();
            Assert.Equal(2, patios.Count);
            Assert.Equal(29, patios.Single(p => p.Id == cenario.Patio.Id).Disponiveis);

            outra.Ativa = false;
            await cenario.Db.SaveChangesAsync();
            Assert.Single(await cenario.Motorista.ListarPatiosAsync());
        }
    }
}
