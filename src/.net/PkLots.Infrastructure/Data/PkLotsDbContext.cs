using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    public class PkLotsDbContext : DbContext
    {
        // Pátio atual resolvido a partir de HttpContext.Items["PatioId"].
        // Quando nulo (migrations, jobs, startup), os filtros não restringem por pátio.
        private readonly Guid? _patioId;

        public DbSet<Patio> Patios { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Vaga> Vagas { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<EntradaVeiculo> EntradasVeiculos { get; set; }
        public DbSet<SaidaVeiculo> SaidasVeiculos { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }

        public PkLotsDbContext(DbContextOptions<PkLotsDbContext> options, IHttpContextAccessor? httpContext = null)
            : base(options)
        {
            var valor = httpContext?.HttpContext?.Items["PatioId"]?.ToString();
            _patioId = Guid.TryParse(valor, out var id) ? id : null;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Filtros multi-pátio (multi-tenant)
            modelBuilder.Entity<Vaga>()
                .HasQueryFilter(v => _patioId == null || v.PatioId == _patioId);

            modelBuilder.Entity<Usuario>()
                .HasQueryFilter(u => _patioId == null || u.PatioId == _patioId);

            modelBuilder.Entity<Veiculo>()
                .HasQueryFilter(v => _patioId == null || v.PatioId == _patioId);

            modelBuilder.Entity<EntradaVeiculo>()
                .HasQueryFilter(e => _patioId == null || e.PatioId == _patioId);

            modelBuilder.Entity<SaidaVeiculo>()
                .HasQueryFilter(s => _patioId == null || s.PatioId == _patioId);

            modelBuilder.Entity<LogAuditoria>()
                .HasQueryFilter(l => _patioId == null || l.PatioId == _patioId);

            // Índices
            modelBuilder.Entity<Vaga>()
                .HasIndex(v => new { v.PatioId, v.Disponivel });

            modelBuilder.Entity<EntradaVeiculo>()
                .HasIndex(e => new { e.PatioId, e.DataHoraEntrada });

            modelBuilder.Entity<Veiculo>()
                .HasIndex(v => v.Placa);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email).IsUnique();

            // Relacionamentos
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Patio)
                .WithMany(p => p.Usuarios)
                .HasForeignKey(u => u.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vaga>()
                .HasOne(v => v.Patio)
                .WithMany(p => p.Vagas)
                .HasForeignKey(v => v.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Veiculo>()
                .HasOne(v => v.Patio)
                .WithMany(p => p.Veiculos)
                .HasForeignKey(v => v.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EntradaVeiculo>()
                .HasOne(e => e.Patio)
                .WithMany(p => p.Entradas)
                .HasForeignKey(e => e.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SaidaVeiculo>()
                .HasOne(s => s.Patio)
                .WithMany(p => p.Saidas)
                .HasForeignKey(s => s.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            // LogAuditoria.Detalhes armazenado como jsonb
            modelBuilder.Entity<LogAuditoria>()
                .Property(l => l.Detalhes)
                .HasColumnType("jsonb")
                .HasConversion(
                    d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                    s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>(),
                    new ValueComparer<Dictionary<string, object>>(
                        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                        d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
                        d => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(d, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
        }
    }
}
