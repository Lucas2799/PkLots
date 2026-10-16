using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PkLots.Infrastructure.Data;

namespace PkLots.Integracao.Tests
{
    public class MigrationsTests
    {
        // Mudou uma entidade e esqueceu o "dotnet ef migrations add"? O deploy subiria com o banco diferente do código.
        [Fact]
        public void Migrations_cobrem_todo_o_modelo()
        {
            using var db = new PkLotsDbContext(new DbContextOptionsBuilder<PkLotsDbContext>()
                .UseNpgsql(PostgresDeTeste.ConexaoDoBanco("postgres")).Options);

            var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
            Assert.NotNull(snapshot);
            if (snapshot is IMutableModel mutavel)
                snapshot = mutavel.FinalizeModel();
            snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

            var diferencas = db.GetService<IMigrationsModelDiffer>().GetDifferences(
                snapshot.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

            Assert.True(diferencas.Count == 0,
                "Modelo diferente das migrations. Crie uma migration: " + string.Join("; ", diferencas.Select(d => d.GetType().Name)));
        }

        // As migrations sobem do zero (banco novo de cliente) e o banco fica sem migrations pendentes
        [Fact]
        public async Task Banco_novo_fica_com_todas_as_migrations()
        {
            var banco = PostgresDeTeste.NovoBanco();
            try
            {
                await using var db = new PkLotsDbContext(banco.Opcoes);
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
                Assert.Single(await db.Planos.Where(p => p.Nome == "Cortesia").ToListAsync());
            }
            finally
            {
                banco.Descartar();
            }
        }

        // Em um banco com dados (cliente já operando), a última migration desfaz e refaz sem erro
        [Fact]
        public async Task Ultima_migration_desfaz_e_refaz()
        {
            var banco = PostgresDeTeste.NovoBanco();
            try
            {
                await using var db = new PkLotsDbContext(banco.Opcoes);
                var migracoes = db.Database.GetMigrations().ToList();
                var migrador = db.GetService<IMigrator>();

                await migrador.MigrateAsync(migracoes[^2]);
                Assert.Equal([migracoes[^1]], await db.Database.GetPendingMigrationsAsync());
                await migrador.MigrateAsync(migracoes[^1]);
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
            finally
            {
                NpgsqlConnection.ClearAllPools();
                banco.Descartar();
            }
        }
    }
}
