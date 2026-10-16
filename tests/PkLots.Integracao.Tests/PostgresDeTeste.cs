using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PkLots.Application.Tests;
using PkLots.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace PkLots.Integracao.Tests
{
    // Um PostgreSQL de verdade (container descartável) para toda a execução dos testes.
    // As migrations rodam uma vez num banco modelo; cada cenário ganha uma cópia dele (CREATE DATABASE ... TEMPLATE),
    // apagada ao final do teste.
    public static class PostgresDeTeste
    {
        private const string Modelo = "pklots_modelo";
        // Task.Run: fora do contexto de sincronização do xUnit (bloquear nele trava a execução)
        private static readonly Lazy<string> ConexaoServidor =
            new(() => Task.Run(IniciarAsync).GetAwaiter().GetResult(), LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly object TravaCopia = new();
        private static PostgreSqlContainer? _container;

        // Antes de qualquer teste: o Cenario passa a usar o PostgreSQL
        [ModuleInitializer]
        internal static void Registrar()
        {
            Cenario.FabricaBanco = NovoBanco;
            // Enquanto o container sobe, vários testes esperam por ele: threads de sobra para os que não usam o banco
            ThreadPool.GetMinThreads(out var trabalho, out var es);
            ThreadPool.SetMinThreads(Math.Max(trabalho, 64), Math.Max(es, 64));
        }

        public static BancoDeTeste NovoBanco()
        {
            var nome = $"t_{Guid.NewGuid():N}";
            // Duas cópias do mesmo modelo ao mesmo tempo falham no PostgreSQL ("being accessed by other users")
            lock (TravaCopia)
                Executar($"CREATE DATABASE \"{nome}\" TEMPLATE \"{Modelo}\"");

            var conexao = ConexaoDoBanco(nome);
            var opcoes = new DbContextOptionsBuilder<PkLotsDbContext>().UseNpgsql(conexao).Options;
            return new BancoDeTeste(opcoes, () =>
            {
                NpgsqlConnection.ClearPool(new NpgsqlConnection(conexao));
                Executar($"DROP DATABASE IF EXISTS \"{nome}\" WITH (FORCE)");
            });
        }

        public static string ConexaoDoBanco(string banco) =>
            new NpgsqlConnectionStringBuilder(ConexaoServidor.Value) { Database = banco }.ConnectionString;

        private static async Task<string> IniciarAsync()
        {
            // Podman sem Docker: usa o socket do usuário quando o DOCKER_HOST não foi definido
            var socketPodman = $"/run/user/{Environment.GetEnvironmentVariable("UID") ?? IdUsuario()}/podman/podman.sock";
            if (Environment.GetEnvironmentVariable("DOCKER_HOST") is null && !File.Exists("/var/run/docker.sock") && File.Exists(socketPodman))
                Environment.SetEnvironmentVariable("DOCKER_HOST", $"unix://{socketPodman}");

            // Mesma imagem da produção (docker/podman-compose.yml)
            _container = new PostgreSqlBuilder("docker.io/library/postgres:16-alpine")
                .WithDatabase("postgres")
                .Build();
            await _container.StartAsync();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _container.DisposeAsync().AsTask().GetAwaiter().GetResult();

            var servidor = _container.GetConnectionString();
            await using (var conexao = new NpgsqlConnection(servidor))
            {
                await conexao.OpenAsync();
                await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{Modelo}\"", conexao);
                await comando.ExecuteNonQueryAsync();
            }

            // Banco modelo com todas as migrations, exatamente como no deploy
            var conexaoModelo = new NpgsqlConnectionStringBuilder(servidor) { Database = Modelo }.ConnectionString;
            await using (var db = new PkLotsDbContext(new DbContextOptionsBuilder<PkLotsDbContext>().UseNpgsql(conexaoModelo).Options))
                await db.Database.MigrateAsync();
            NpgsqlConnection.ClearPool(new NpgsqlConnection(conexaoModelo)); // o modelo não pode ter conexões abertas
            return servidor;
        }

        private static void Executar(string sql)
        {
            using var conexao = new NpgsqlConnection(ConexaoServidor.Value);
            conexao.Open();
            using var comando = new NpgsqlCommand(sql, conexao);
            comando.ExecuteNonQuery();
        }

        private static string IdUsuario()
        {
            try
            {
                return File.ReadLines("/proc/self/status").First(l => l.StartsWith("Uid:")).Split('\t', StringSplitOptions.RemoveEmptyEntries)[1];
            }
            catch (Exception)
            {
                return "1000";
            }
        }
    }
}
