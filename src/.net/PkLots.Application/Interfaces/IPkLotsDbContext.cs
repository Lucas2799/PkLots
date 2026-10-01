using Microsoft.EntityFrameworkCore;
using PkLots.Core.Entities;

namespace PkLots.Application.Interfaces
{
    public interface IPkLotsDbContext
    {
        DbSet<Empresa> Empresas { get; }
        DbSet<Patio> Patios { get; }
        DbSet<Usuario> Usuarios { get; }
        DbSet<UsuarioPatio> UsuariosPatios { get; }
        DbSet<VeiculoMotorista> VeiculosMotoristas { get; }
        DbSet<Vaga> Vagas { get; }
        DbSet<Veiculo> Veiculos { get; }
        DbSet<EntradaVeiculo> EntradasVeiculos { get; }
        DbSet<SaidaVeiculo> SaidasVeiculos { get; }
        DbSet<LogAuditoria> LogsAuditoria { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
