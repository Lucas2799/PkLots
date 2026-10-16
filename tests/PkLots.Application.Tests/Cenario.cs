using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using PkLots.Application.DTOs;
using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;
using PkLots.Infrastructure.Data;

namespace PkLots.Application.Tests
{
    // Monta banco em memória com uma empresa, um pátio de 30 vagas e os serviços da aplicação.
    // Por padrão os serviços rodam como admin dessa empresa, no pátio criado.
    public sealed class Cenario : IDisposable
    {
        // Banco de cada cenário: em memória por padrão; os testes de integração trocam por um PostgreSQL real
        public static Func<BancoDeTeste>? FabricaBanco { get; set; }

        private readonly DbContextOptions<PkLotsDbContext> _opcoes;
        private readonly BancoDeTeste? _banco;

        public static readonly DateTimeOffset Inicio = new(2026, 9, 30, 13, 30, 45, TimeSpan.Zero);

        public PkLotsDbContext Db { get; }
        public FakeTimeProvider Relogio { get; } = new(Inicio);
        public Empresa Empresa { get; }
        public Patio Patio { get; }

        // Quem está usando os serviços (ajustável por teste)
        public string PerfilAtual { get; set; } = Perfis.Admin;
        public Guid UsuarioAtualId { get; set; } = Guid.NewGuid();
        public Guid? PatioEscolhidoId { get; set; }
        public LprFalso Lpr { get; } = new();
        public HashSenhaFalso HashSenha { get; } = new();

        public Cenario(int vagasPorFileira = 10)
        {
            _banco = FabricaBanco?.Invoke();
            _opcoes = _banco?.Opcoes ?? new DbContextOptionsBuilder<PkLotsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Db = new PkLotsDbContext(_opcoes);

            // Criados "antes" do relógio simulado, como dados já existentes
            var criacao = Inicio.UtcDateTime.AddDays(-1);
            Empresa = new Empresa { Nome = "Empresa Teste", CriadoEm = criacao };
            Patio = new Patio { EmpresaId = Empresa.Id, Nome = "Pátio Teste", ValorHora = 15.00m, CriadoEm = criacao };
            Db.Empresas.Add(Empresa);
            Db.Patios.Add(Patio);
            // Como a migration: plano Cortesia (sem limites) e a empresa do cenário nele (no PostgreSQL a migration já criou)
            if (!Db.Planos.Any(p => p.Id == RecursosPlano.PlanoCortesiaId))
                Db.Planos.Add(new Plano
                {
                    Id = RecursosPlano.PlanoCortesiaId, Nome = "Cortesia", PrecoPorPatio = 0, Recursos = [.. RecursosPlano.Todos], CriadoEm = criacao
                });
            Db.Assinaturas.Add(new Assinatura { EmpresaId = Empresa.Id, PlanoId = RecursosPlano.PlanoCortesiaId, CriadoEm = criacao });
            // Quem usa os serviços precisa existir no banco (no PostgreSQL caixas e pagamentos apontam para ele).
            // Conta inativa e fora da empresa: não entra em listas, limites nem avisos
            Db.Usuarios.Add(new Usuario
            {
                Id = UsuarioAtualId, Email = "cenario@pklots.teste", SenhaHash = "-", NomeCompleto = "Usuário do Cenário",
                Perfil = Perfis.Motorista, Ativo = false, CriadoEm = criacao
            });
            foreach (var fileira in new[] { 'A', 'B', 'C' })
                for (var i = 1; i <= vagasPorFileira; i++)
                    Db.Vagas.Add(new Vaga { PatioId = Patio.Id, Numero = $"{fileira}{i}", Fileira = fileira, Indice = i });
            Db.SaveChanges();
        }

        // Outra conexão com o mesmo banco, como uma segunda requisição (testes de concorrência)
        public PkLotsDbContext NovoDb() => new(_opcoes);

        public IContextoPatio ContextoPatio =>
            new ContextoPatioTeste(Db, PerfilAtual is Perfis.Admin or Perfis.Operador ? Empresa.Id : null, UsuarioAtualId, PerfilAtual, PatioEscolhidoId);

        // DbContext como numa requisição de um usuário da empresa informada (filtros multi-tenant ativos)
        public PkLotsDbContext DbDaEmpresa(Guid empresaId)
        {
            var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(PkLotsDbContext.ClaimEmpresa, empresaId.ToString())], "teste"));
            var acessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = usuario } };
            return new PkLotsDbContext(_opcoes, acessor);
        }

        // Outra empresa com um pátio (para testes de isolamento)
        public (Empresa Empresa, Patio Patio) CriarOutraEmpresa(string nome = "Outra Empresa")
        {
            var empresa = new Empresa { Nome = nome };
            var patio = ServicoPatios.NovoPatio(empresa.Id, $"Pátio {nome}", 5, DateTime.UtcNow);
            Db.Empresas.Add(empresa);
            Db.Patios.Add(patio);
            Db.SaveChanges();
            return (empresa, patio);
        }
        public ICacheVagas Cache => new CacheFalso();
        public LinksPublicos Links { get; } = new("https://pklots.teste");
        public GeradorTokenFalso GeradorToken { get; } = new();

        public ReconhecedorPlaca Reconhecedor => new(Lpr, Db, ContextoPatio, Relogio);
        public ServicoEntrada Entrada => new(Db, ContextoPatio, Cache, Reconhecedor, Relogio);
        public ServicoSaida Saida => new(Db, ContextoPatio, Cache, Reconhecedor, Relogio);
        public ServicoVagas Vagas => new(Db, ContextoPatio, Cache, Relogio);
        public ServicoVeiculos Veiculos => new(Db, ContextoPatio);
        public ServicoMotorista Motorista => new(Db, Veiculos, Vagas, Relogio);
        public ServicoCaixa Caixa => new(Db, ContextoPatio, Relogio);
        public ServicoConfiguracaoPagamento ConfiguracaoPagamento => new(Db, ContextoPatio, Relogio);

        // Saída completa como no balcão: abre o caixa (se preciso), gera a cobrança e confirma o pagamento
        public async Task<SaidaDto> SairAsync(string placa, string forma = FormasPagamento.Dinheiro, decimal? recebido = null)
        {
            var cobranca = await Saida.CobrarAsync(new GerarCobrancaRequisicao(placa, null));
            return await ConfirmarAsync(cobranca, forma, recebido);
        }

        public async Task<SaidaDto> SairPorTicketAsync(string codigo, string forma = FormasPagamento.Dinheiro)
        {
            var cobranca = await Saida.CobrarAsync(new GerarCobrancaRequisicao(null, codigo));
            return await ConfirmarAsync(cobranca, forma, null);
        }

        private async Task<SaidaDto> ConfirmarAsync(CobrancaDto cobranca, string forma, decimal? recebido)
        {
            if (cobranca.RequerPagamento && await Caixa.ObterAtualAsync() is null)
                await Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            return await Saida.ConfirmarAsync(UsuarioAtualId, cobranca.Id, new ConfirmarSaidaRequisicao(forma, recebido));
        }
        public ServicoPatios Patios => new(Db, ContextoPatio, Relogio);
        public ServicoPlataforma Plataforma => new(Db, HashSenha, Links, Relogio);
        public ServicoMensalistas Mensalistas => new(Db, ContextoPatio, Links, Relogio);
        public ServicoAcessoMensalista AcessoMensalista => new(Db, HashSenha, GeradorToken, Relogio);
        public ServicoEstadiaPublica EstadiaPublica => new(Db, Relogio);
        public ServicoDashboard Dashboard => new(Db, ContextoPatio, Vagas, Mensalistas, Relogio);
        public ServicoRelatorios Relatorios => new(Db, ContextoPatio, Relogio);
        public ServicoPlanos Planos => new(Db, ContextoPatio, Relogio);
        public ServicoFaturas Faturas => new(Db, ContextoPatio, Links, Relogio);
        public ServicoTabelaPreco TabelaPreco => new(Db, ContextoPatio, Relogio);
        public ImpressoraFalsa Impressora { get; } = new();
        public ServicoImpressao Impressao =>
            new(Db, ContextoPatio, Impressora, Links, Relogio, Microsoft.Extensions.Logging.Abstractions.NullLogger<ServicoImpressao>.Instance);
        public ServicoAdministracao Administracao => new(Db, ContextoPatio, Cache, HashSenha, Links, Relogio);
        public ServicoAutenticacao Autenticacao => new(Db, HashSenha, GeradorToken, Links, Relogio);

        // Token que iria no link do último e-mail enviado para o endereço (lido do corpo do e-mail na fila)
        public string TokenDoUltimoEmail(string para, string parametro)
        {
            var email = Db.EmailsPendentes.Where(e => e.Para == para).OrderBy(e => e.CriadoEm).AsEnumerable().Last();
            var marcador = $"?{parametro}=";
            var inicio = email.CorpoTexto!.IndexOf(marcador, StringComparison.Ordinal) + marcador.Length;
            return new string(email.CorpoTexto[inicio..].TakeWhile(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        }

        // admin/operador pertencem à empresa (operador vinculado ao pátio); motorista é conta da plataforma
        public Usuario CriarUsuario(string email, string senha, string perfil = Perfis.Motorista)
        {
            var daEmpresa = perfil is Perfis.Admin or Perfis.Operador;
            var usuario = new Usuario
            {
                EmpresaId = daEmpresa ? Empresa.Id : null,
                Email = email,
                SenhaHash = HashSenha.Gerar(senha),
                NomeCompleto = "Usuário Teste",
                Perfil = perfil
            };
            if (perfil == Perfis.Operador)
                usuario.Patios.Add(new UsuarioPatio { PatioId = Patio.Id });
            Db.Usuarios.Add(usuario);
            Db.SaveChanges();
            return usuario;
        }

        public void Dispose()
        {
            Db.Dispose();
            _banco?.Descartar();
        }
    }

    // Banco de um cenário e como descartá-lo ao final do teste
    public record BancoDeTeste(DbContextOptions<PkLotsDbContext> Opcoes, Action Descartar);

    // Mesmas regras do ContextoPatio real (admin: pátios da empresa; operador: vinculados), sem HTTP
    public class ContextoPatioTeste(PkLotsDbContext db, Guid? empresaId, Guid? usuarioId, string? perfil, Guid? patioEscolhido) : IContextoPatio
    {
        public Guid? EmpresaId => empresaId;
        public Guid? UsuarioId => usuarioId;
        public string? Perfil => perfil;

        public async Task<Guid> ObterPatioIdAsync(CancellationToken ct = default)
        {
            var permitidos = await ObterPatiosPermitidosAsync(ct);
            if (patioEscolhido is not null)
                return permitidos.Contains(patioEscolhido.Value)
                    ? patioEscolhido.Value
                    : throw new PkLots.Application.Excecoes.ExcecaoProibido("Você não tem acesso a este pátio.");
            return permitidos.Count > 0 ? permitidos[0] : throw new PkLots.Application.Excecoes.ExcecaoNaoEncontrado("Nenhum pátio disponível.");
        }

        public async Task<IReadOnlyList<Guid>> ObterPatiosPermitidosAsync(CancellationToken ct = default)
        {
            if (empresaId is null)
                return [];
            var consulta = db.Patios.Where(p => p.EmpresaId == empresaId && p.Ativo);
            if (perfil == Perfis.Operador)
                consulta = consulta.Where(p => db.UsuariosPatios.Any(up => up.UsuarioId == usuarioId && up.PatioId == p.Id));
            return await consulta.OrderBy(p => p.CriadoEm).ThenBy(p => p.Nome).Select(p => p.Id).ToListAsync(ct);
        }
    }

    public class CacheFalso : ICacheVagas
    {
        public Task<ResumoVagasDto?> ObterAsync(Guid patioId) => Task.FromResult<ResumoVagasDto?>(null);
        public Task DefinirAsync(Guid patioId, ResumoVagasDto resumo) => Task.CompletedTask;
        public Task InvalidarAsync(Guid patioId) => Task.CompletedTask;
    }

    public class LprFalso : IServicoLpr
    {
        public ResultadoLprDto Resultado { get; set; } = new(true, "ABC1234", 0.95m, "easyocr");

        public Task<ResultadoLprDto> AnalisarAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default) =>
            Task.FromResult(Resultado);
    }

    public class ImpressoraFalsa : IImpressoraTickets
    {
        public List<(string Host, int Porta, byte[] Dados)> Enviados { get; } = [];
        public bool Falhar { get; set; }

        public Task EnviarAsync(string host, int porta, byte[] dados, CancellationToken ct = default)
        {
            if (Falhar)
                throw new System.Net.Sockets.SocketException(111); // conexão recusada
            Enviados.Add((host, porta, dados));
            return Task.CompletedTask;
        }
    }

    public class HashSenhaFalso : IHashSenha
    {
        public string Gerar(string senha) => $"hash:{senha}";
        public bool Verificar(string senha, string hash) => hash == $"hash:{senha}";
    }

    public class GeradorTokenFalso : IGeradorToken
    {
        public TokenDto Gerar(Usuario usuario) => new($"token-{usuario.Id}", 86400, usuario.Perfil, usuario.DeveTrocarSenha);
    }
}
