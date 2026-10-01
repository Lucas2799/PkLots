using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    public class OpcoesPlataforma
    {
        public string SuperAdminEmail { get; set; } = "superadmin@pklots.local";
        public string? SuperAdminSenha { get; set; }
    }

    // Dados iniciais: superadmin da plataforma e, em banco vazio, uma empresa de demonstração
    // (pátio com 30 vagas, admin, operador e um motorista)
    public static class SemeadorDados
    {
        private const string SenhaPadraoSuperAdmin = "SuperAdmin@123";

        public static async Task SemearAsync(PkLotsDbContext db, IHashSenha hashSenha, OpcoesPlataforma opcoes,
            ILogger? logger = null, CancellationToken ct = default)
        {
            await GarantirSuperAdminAsync(db, hashSenha, opcoes, logger, ct);

            if (await db.Empresas.IgnoreQueryFilters().AnyAsync(ct))
                return;

            var agora = DateTime.UtcNow;
            var empresa = new Empresa { Nome = "Empresa Padrão" };
            var patio = ServicoPatios.NovoPatio(empresa.Id, "Pátio Padrão", 30, agora);
            db.Empresas.Add(empresa);
            db.Patios.Add(patio);

            var operador = NovoUsuario(empresa.Id, "operator@pklots.local", "Operator@123", "Operador", Perfis.Operador, hashSenha);
            operador.Patios.Add(new UsuarioPatio { PatioId = patio.Id });

            db.Usuarios.AddRange(
                NovoUsuario(empresa.Id, "admin@pklots.local", "Admin@123", "Administrador", Perfis.Admin, hashSenha),
                operador,
                NovoUsuario(null, "driver@example.com", "Driver@123", "Motorista Exemplo", Perfis.Motorista, hashSenha));

            await db.SaveChangesAsync(ct);
        }

        private static async Task GarantirSuperAdminAsync(PkLotsDbContext db, IHashSenha hashSenha, OpcoesPlataforma opcoes,
            ILogger? logger, CancellationToken ct)
        {
            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Perfil == Perfis.SuperAdmin, ct))
                return;

            var senha = opcoes.SuperAdminSenha;
            if (string.IsNullOrWhiteSpace(senha))
            {
                senha = SenhaPadraoSuperAdmin;
                logger?.LogWarning("Superadmin criado com a senha padrão. Defina Plataforma__SuperAdminSenha e troque a senha.");
            }

            db.Usuarios.Add(NovoUsuario(null, opcoes.SuperAdminEmail.Trim().ToLowerInvariant(), senha, "Administrador da Plataforma",
                Perfis.SuperAdmin, hashSenha));
            await db.SaveChangesAsync(ct);
        }

        private static Usuario NovoUsuario(Guid? empresaId, string email, string senha, string nome, string perfil, IHashSenha hashSenha) =>
            new()
            {
                EmpresaId = empresaId,
                Email = email,
                SenhaHash = hashSenha.Gerar(senha),
                NomeCompleto = nome,
                Perfil = perfil
            };
    }
}
