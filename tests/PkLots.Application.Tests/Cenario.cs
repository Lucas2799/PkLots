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
        private readonly DbContextOptions<PkLotsDbContext> _opcoes;

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
            _opcoes = new DbContextOptionsBuilder<PkLotsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Db = new PkLotsDbContext(_opcoes);

            // Criados "antes" do relógio simulado, como dados já existentes
            var criacao = Inicio.UtcDateTime.AddDays(-1);
            Empresa = new Empresa { Nome = "Empresa Teste", CriadoEm = criacao };
            Patio = new Patio { EmpresaId = Empresa.Id, Nome = "Pátio Teste", ValorHora = 15.00m, CriadoEm = criacao };
            Db.Empresas.Add(Empresa);
            Db.Patios.Add(Patio);
            foreach (var fileira in new[] { 'A', 'B', 'C' })
                for (var i = 1; i <= vagasPorFileira; i++)
                    Db.Vagas.Add(new Vaga { PatioId = Patio.Id, Numero = $"{fileira}{i}", Fileira = fileira, Indice = i });
            Db.SaveChanges();
        }

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

        public ReconhecedorPlaca Reconhecedor => new(Lpr, Db, ContextoPatio, Relogio);
        public ServicoEntrada Entrada => new(Db, ContextoPatio, Cache, Reconhecedor, Relogio);
        public ServicoSaida Saida => new(Db, ContextoPatio, Cache, Reconhecedor, Relogio);
        public ServicoVagas Vagas => new(Db, ContextoPatio, Cache, Relogio);
        public ServicoVeiculos Veiculos => new(Db, ContextoPatio);
        public ServicoMotorista Motorista => new(Db, Veiculos, Saida, Vagas, Relogio);
        public ServicoPatios Patios => new(Db, ContextoPatio, Relogio);
        public ServicoPlataforma Plataforma => new(Db, HashSenha, Relogio);
        public ServicoMensalistas Mensalistas => new(Db, ContextoPatio, Relogio);
        public ServicoDashboard Dashboard => new(Db, ContextoPatio, Vagas, Mensalistas, Relogio);
        public ImpressoraFalsa Impressora { get; } = new();
        public ServicoImpressao Impressao =>
            new(Db, ContextoPatio, Impressora, Relogio, Microsoft.Extensions.Logging.Abstractions.NullLogger<ServicoImpressao>.Instance);
        public ServicoAdministracao Administracao => new(Db, ContextoPatio, Cache, HashSenha, Relogio);
        public ServicoAutenticacao Autenticacao => new(Db, HashSenha, new GeradorTokenFalso(), Relogio);

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

        public void Dispose() => Db.Dispose();
    }

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
        public TokenDto Gerar(Usuario usuario) => new($"token-{usuario.Id}", 86400, usuario.Perfil);
    }
}
