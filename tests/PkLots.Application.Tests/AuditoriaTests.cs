using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;
using PkLots.Infrastructure.Servicos;

namespace PkLots.Application.Tests
{
    public class AuditoriaTests
    {
        private static ServicoAuditoria Auditoria(Cenario cenario) => new(cenario.Db, cenario.ContextoPatio, cenario.Relogio);

        private static LogAuditoria Log(Guid patioId, string acao, DateTime quando, Guid? usuarioId = null, string? placa = null,
            string situacao = SituacaoAuditoria.Sucesso, Dictionary<string, object>? detalhes = null) =>
            new() { PatioId = patioId, Acao = acao, Situacao = situacao, Placa = placa, UsuarioId = usuarioId, CriadoEm = quando, Detalhes = detalhes ?? [] };

        [Fact]
        public async Task Admin_ve_o_que_aconteceu_na_empresa_com_quem_fez_e_detalhes_legiveis()
        {
            using var cenario = new Cenario();
            var dono = cenario.CriarUsuario("dono@pklots.local", "Senha@123", Perfis.Admin);
            cenario.UsuarioAtualId = dono.Id;
            await cenario.TabelaPreco.SalvarAsync(dono.Id, new SalvarTabelaPrecoRequisicao(new RegrasPreco(ValorHora: 15m), null, 15));
            cenario.Relogio.Advance(TimeSpan.FromMinutes(5));
            await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("abc1d23"));
            // Sem requisição HTTP nos testes, quem fez é informado à mão (na API o DbContext preenche)
            foreach (var log in cenario.Db.LogsAuditoria)
                log.UsuarioId = dono.Id;
            var (_, patioOutra) = cenario.CriarOutraEmpresa();
            cenario.Db.LogsAuditoria.Add(Log(patioOutra.Id, AcoesAuditoria.EntradaSucesso, cenario.Relogio.GetUtcNow().UtcDateTime, placa: "OUT1234"));
            await cenario.Db.SaveChangesAsync();

            var pagina = await Auditoria(cenario).ListarAsync(new FiltroAuditoria());

            Assert.Equal(2, pagina.Total); // o registro da outra empresa não aparece
            var (entrada, configuracao) = (pagina.Itens[0], pagina.Itens[1]); // mais recente primeiro
            Assert.Equal(("Entrada registrada", CatalogoAuditoria.Movimento, "ABC1D23", "Usuário Teste", "Pátio Teste"),
                (entrada.Descricao, entrada.Categoria, entrada.Placa, entrada.Usuario, entrada.Patio));
            Assert.Contains(new DetalheAuditoriaDto("Vaga", "A1"), entrada.Detalhes);
            Assert.Null(entrada.Empresa); // admin não precisa do nome da própria empresa
            Assert.Equal("Configuração do pátio alterada", configuracao.Descricao);
            Assert.Contains(new DetalheAuditoriaDto("Tolerância (min)", "0 → 15"), configuracao.Detalhes);
            Assert.DoesNotContain(configuracao.Detalhes, d => d.Rotulo == "adminId"); // identificadores internos ficam de fora
        }

        [Fact]
        public async Task Filtra_por_categoria_placa_situacao_usuario_e_pagina()
        {
            using var cenario = new Cenario();
            var operador = cenario.CriarUsuario("op@pklots.local", "Senha@123", Perfis.Operador);
            var agora = cenario.Relogio.GetUtcNow().UtcDateTime;
            var patio = cenario.Patio.Id;
            cenario.Db.LogsAuditoria.AddRange(
                Log(patio, AcoesAuditoria.EntradaSucesso, agora.AddMinutes(-1), operador.Id, "ABC1234"),
                Log(patio, AcoesAuditoria.EntradaRecusada, agora.AddMinutes(-2), operador.Id, "XYZ9876", SituacaoAuditoria.Alerta),
                Log(patio, AcoesAuditoria.CaixaAberto, agora.AddMinutes(-3), operador.Id),
                Log(patio, AcoesAuditoria.ConfiguracaoAlterada, agora.AddMinutes(-4)),
                Log(patio, AcoesAuditoria.EntradaSucesso, agora.AddDays(-10), operador.Id, "ABC1234")); // fora dos 7 dias padrão
            await cenario.Db.SaveChangesAsync();
            var servico = Auditoria(cenario);

            Assert.Equal(4, (await servico.ListarAsync(new FiltroAuditoria())).Total);
            Assert.Equal(2, (await servico.ListarAsync(new FiltroAuditoria(Categoria: CatalogoAuditoria.Movimento))).Total);
            Assert.Equal("ABC1234", Assert.Single((await servico.ListarAsync(new FiltroAuditoria(Placa: "abc-12"))).Itens).Placa);
            Assert.Equal("XYZ9876", Assert.Single((await servico.ListarAsync(new FiltroAuditoria(Situacao: "alerta"))).Itens).Placa);
            Assert.Equal(3, (await servico.ListarAsync(new FiltroAuditoria(UsuarioId: operador.Id))).Total);
            Assert.Equal(ServicoAuditoria.NomeAutomatico,
                (await servico.ListarAsync(new FiltroAuditoria(Categoria: CatalogoAuditoria.Configuracao))).Itens.Single().Usuario);
            Assert.Equal(5, (await servico.ListarAsync(new FiltroAuditoria(De: new DateOnly(2026, 9, 1)))).Total);

            var segunda = await servico.ListarAsync(new FiltroAuditoria(Pagina: 2, TamanhoPagina: 3));
            Assert.Equal((4, 1), (segunda.Total, segunda.Itens.Count));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => servico.ListarAsync(new FiltroAuditoria(De: new DateOnly(2025, 1, 1))));
        }

        [Fact]
        public async Task Superadmin_ve_todas_as_empresas_e_admin_ve_a_equipe_pklots_sem_nome()
        {
            using var cenario = new Cenario();
            var suporte = cenario.CriarUsuario("suporte@pklots.com", "Senha@123", Perfis.SuperAdmin);
            suporte.NomeCompleto = "Ana Suporte";
            var (outra, patioOutra) = cenario.CriarOutraEmpresa("Estacionamentos Silva");
            var agora = cenario.Relogio.GetUtcNow().UtcDateTime;
            cenario.Db.LogsAuditoria.AddRange(
                Log(cenario.Patio.Id, AcoesAuditoria.AssinaturaAlterada, agora, suporte.Id),
                Log(patioOutra.Id, AcoesAuditoria.FaturaPaga, agora.AddMinutes(-1), suporte.Id));
            await cenario.Db.SaveChangesAsync();

            var doAdmin = Assert.Single((await Auditoria(cenario).ListarAsync(new FiltroAuditoria())).Itens);
            Assert.Equal(ServicoAuditoria.NomeSuporte, doAdmin.Usuario);
            // Admin não escolhe empresa: o filtro é ignorado
            Assert.Equal(1, (await Auditoria(cenario).ListarAsync(new FiltroAuditoria(EmpresaId: outra.Id))).Total);

            cenario.PerfilAtual = Perfis.SuperAdmin;
            var todas = await Auditoria(cenario).ListarAsync(new FiltroAuditoria());
            Assert.Equal(["Empresa Teste", "Estacionamentos Silva"], todas.Itens.Select(i => i.Empresa));
            Assert.Equal("Ana Suporte (Equipe PkLots)", todas.Itens[0].Usuario);
            Assert.Equal("Fatura paga", Assert.Single((await Auditoria(cenario).ListarAsync(new FiltroAuditoria(EmpresaId: outra.Id))).Itens).Descricao);

            cenario.PerfilAtual = Perfis.Operador;
            await Assert.ThrowsAsync<ExcecaoProibido>(() => Auditoria(cenario).ListarAsync(new FiltroAuditoria()));
        }

        [Fact]
        public async Task Opcoes_dos_filtros_por_perfil()
        {
            using var cenario = new Cenario();
            cenario.CriarUsuario("op@pklots.local", "Senha@123", Perfis.Operador);
            var (outra, _) = cenario.CriarOutraEmpresa();

            var doAdmin = await Auditoria(cenario).OpcoesAsync(null);
            Assert.Equal(["Pátio Teste"], doAdmin.Patios.Select(p => p.Nome));
            Assert.Equal(["Usuário Teste"], doAdmin.Usuarios.Select(u => u.Nome));
            Assert.Empty(doAdmin.Empresas);
            Assert.Equal(CatalogoAuditoria.Categorias.Count, doAdmin.Categorias.Count);

            cenario.PerfilAtual = Perfis.SuperAdmin;
            var semEmpresa = await Auditoria(cenario).OpcoesAsync(null);
            Assert.Equal(2, semEmpresa.Empresas.Count);
            Assert.Empty(semEmpresa.Patios); // pátios e usuários aparecem depois de escolher a empresa
            Assert.Equal(["Pátio Outra Empresa"], (await Auditoria(cenario).OpcoesAsync(outra.Id)).Patios.Select(p => p.Nome));
        }

        [Fact]
        public void Detalhes_formatam_dinheiro_numeros_e_sim_nao_no_padrao_brasileiro()
        {
            var detalhes = CatalogoAuditoria.Detalhes(new Dictionary<string, object>
            {
                ["valor"] = 1234.5m, ["confianca"] = 0.95m, ["ativo"] = false, ["ativoAnterior"] = true, ["usuarioId"] = Guid.NewGuid().ToString(),
                ["placas"] = JsonSerializer.SerializeToElement(new[] { "ABC1234", "BRA2E19" })
            });

            Assert.Equal(
                [new("Valor", "R$ 1.234,50"), new("Confiança da leitura", "0,95"), new("Ativo", "Sim → Não"), new DetalheAuditoriaDto("Placas", "ABC1234, BRA2E19")],
                detalhes);
        }

        [Fact]
        public async Task Gravacao_registra_o_usuario_logado_sem_o_servico_informar()
        {
            using var cenario = new Cenario();
            var usuarioId = Guid.NewGuid();
            await using (var db = cenario.DbDaEmpresa(cenario.Empresa.Id, usuarioId))
            {
                db.LogsAuditoria.Add(Log(cenario.Patio.Id, AcoesAuditoria.VagaLiberada, DateTime.UtcNow));
                db.LogsAuditoria.Add(Log(cenario.Patio.Id, AcoesAuditoria.VagaManutencao, DateTime.UtcNow, usuarioId: cenario.UsuarioAtualId));
                await db.SaveChangesAsync();
            }

            var logs = await cenario.Db.LogsAuditoria.ToListAsync();
            Assert.Equal(usuarioId, logs.Single(l => l.Acao == AcoesAuditoria.VagaLiberada).UsuarioId);
            Assert.Equal(cenario.UsuarioAtualId, logs.Single(l => l.Acao == AcoesAuditoria.VagaManutencao).UsuarioId); // informado: mantém
        }
    }

    public class LogsSistemaTests : IDisposable
    {
        private readonly string _pasta = Path.Combine(Path.GetTempPath(), "pklots-logs-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_pasta))
                Directory.Delete(_pasta, true);
        }

        [Fact]
        public async Task Le_os_arquivos_do_serilog_filtrando_nivel_texto_e_periodo()
        {
            Directory.CreateDirectory(_pasta);
            // 30/09 13:30 UTC (10:30 em Brasília) é o "agora" do cenário
            File.WriteAllLines(Path.Combine(_pasta, "pklots-20260930.json"),
            [
                """{"@t":"2026-09-30T13:00:00.000Z","@m":"HTTP GET /api/vagas responded 200","RequestPath":"/api/vagas","StatusCode":200,"SourceContext":"Serilog.AspNetCore.RequestLoggingMiddleware"}""",
                """{"@t":"2026-09-30T13:10:00.000Z","@l":"Warning","@m":"Falha ao enviar e-mail para joao@x.com","SourceContext":"PkLots.Infrastructure.Email.ProcessadorFilaEmails"}""",
                """{"@t":"2026-09-30T13:20:00.000Z","@l":"Error","@m":"Erro ao processar POST /api/saidas","@x":"System.InvalidOperationException: falhou","RequestPath":"/api/saidas"}""",
                """{"@t":"2026-09-30T13:2""", // linha cortada no meio da gravação
            ]);
            File.WriteAllLines(Path.Combine(_pasta, "pklots-20260920.json"),
                ["""{"@t":"2026-09-20T12:00:00.000Z","@l":"Error","@m":"Erro antigo"}"""]);
            var leitor = new LeitorLogsArquivo(_pasta, new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Cenario.Inicio));

            var hoje = await leitor.LerAsync(new FiltroLogsSistema());
            Assert.Equal(["Error", "Warning", "Information"], hoje.Itens.Select(i => i.Nivel)); // mais recente primeiro
            Assert.Equal(("/api/saidas", "System.InvalidOperationException: falhou"), (hoje.Itens[0].Caminho, hoje.Itens[0].Excecao));
            Assert.Equal(200, hoje.Itens[2].Status);

            Assert.Equal(2, (await leitor.LerAsync(new FiltroLogsSistema(Nivel: "warning"))).Total);
            Assert.Equal("Warning", Assert.Single((await leitor.LerAsync(new FiltroLogsSistema(Texto: "E-MAIL"))).Itens).Nivel);
            Assert.Equal("Erro antigo", Assert.Single((await leitor.LerAsync(
                new FiltroLogsSistema(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20)))).Itens).Mensagem);
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => leitor.LerAsync(new FiltroLogsSistema(new DateOnly(2026, 9, 1))));
            await Assert.ThrowsAsync<ExcecaoNegocio>(() => leitor.LerAsync(new FiltroLogsSistema(Nivel: "tudo")));
        }

        [Fact]
        public async Task Sem_pasta_de_logs_avisa_que_nao_ha_log_disponivel()
        {
            var leitor = new LeitorLogsArquivo(_pasta, TimeProvider.System);
            var resultado = await leitor.LerAsync(new FiltroLogsSistema());
            Assert.False(resultado.Disponivel);
            Assert.Empty(resultado.Itens);
        }
    }
}
