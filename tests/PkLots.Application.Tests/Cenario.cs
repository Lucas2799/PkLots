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
    // Monta banco em memória com um pátio de 30 vagas e os serviços da aplicação
    public sealed class Cenario : IDisposable
    {
        public static readonly DateTimeOffset Inicio = new(2026, 9, 30, 13, 30, 45, TimeSpan.Zero);

        public PkLotsDbContext Db { get; }
        public FakeTimeProvider Relogio { get; } = new(Inicio);
        public Patio Patio { get; }
        public LprFalso Lpr { get; } = new();
        public HashSenhaFalso HashSenha { get; } = new();

        public Cenario(int vagasPorFileira = 10)
        {
            var opcoes = new DbContextOptionsBuilder<PkLotsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Db = new PkLotsDbContext(opcoes);

            Patio = new Patio { Nome = "Pátio Teste", ValorHora = 15.00m };
            Db.Patios.Add(Patio);
            foreach (var fileira in new[] { 'A', 'B', 'C' })
                for (var i = 1; i <= vagasPorFileira; i++)
                    Db.Vagas.Add(new Vaga { PatioId = Patio.Id, Numero = $"{fileira}{i}", Fileira = fileira, Indice = i });
            Db.SaveChanges();
        }

        public IContextoPatio ContextoPatio => new ContextoPatioFixo(Patio.Id);
        public ICacheVagas Cache => new CacheFalso();

        public ReconhecedorPlaca Reconhecedor => new(Lpr, Db, ContextoPatio, Relogio);
        public ServicoEntrada Entrada => new(Db, ContextoPatio, Cache, Reconhecedor, Relogio);
        public ServicoSaida Saida => new(Db, ContextoPatio, Cache, Reconhecedor, Relogio);
        public ServicoVagas Vagas => new(Db, ContextoPatio, Cache, Relogio);
        public ServicoVeiculos Veiculos => new(Db, ContextoPatio);
        public ServicoMotorista Motorista => new(Db, ContextoPatio, Veiculos, Saida, Relogio);
        public ServicoDashboard Dashboard => new(Db, ContextoPatio, Vagas, Relogio);
        public ImpressoraFalsa Impressora { get; } = new();
        public ServicoImpressao Impressao =>
            new(Db, ContextoPatio, Impressora, Relogio, Microsoft.Extensions.Logging.Abstractions.NullLogger<ServicoImpressao>.Instance);
        public ServicoAdministracao Administracao => new(Db, ContextoPatio, Cache, HashSenha, Relogio);
        public ServicoAutenticacao Autenticacao => new(Db, HashSenha, new GeradorTokenFalso(), ContextoPatio, Relogio);

        public Usuario CriarUsuario(string email, string senha, string perfil = Perfis.Motorista)
        {
            var usuario = new Usuario
            {
                PatioId = Patio.Id,
                Email = email,
                SenhaHash = HashSenha.Gerar(senha),
                NomeCompleto = "Usuário Teste",
                Perfil = perfil
            };
            Db.Usuarios.Add(usuario);
            Db.SaveChanges();
            return usuario;
        }

        public void Dispose() => Db.Dispose();
    }

    public class ContextoPatioFixo(Guid patioId) : IContextoPatio
    {
        public Task<Guid> ObterPatioIdAsync(CancellationToken ct = default) => Task.FromResult(patioId);
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
