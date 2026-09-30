using Microsoft.EntityFrameworkCore;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    // Dados iniciais do MVP: pátio padrão com 30 vagas (A1–C10) e os três usuários padrão
    public static class SemeadorDados
    {
        private static readonly char[] Fileiras = ['A', 'B', 'C'];
        private const int VagasPorFileira = 10;

        public static async Task SemearAsync(PkLotsDbContext db, IHashSenha hashSenha, CancellationToken ct = default)
        {
            if (await db.Patios.AnyAsync(ct))
                return;

            var patio = new Patio
            {
                Nome = "Pátio Padrão",
                Descricao = "Pátio inicial do MVP",
                MaximoVagas = Fileiras.Length * VagasPorFileira,
                ValorHora = 15.00m
            };
            db.Patios.Add(patio);

            foreach (var fileira in Fileiras)
            {
                for (var indice = 1; indice <= VagasPorFileira; indice++)
                {
                    db.Vagas.Add(new Vaga
                    {
                        PatioId = patio.Id,
                        Numero = $"{fileira}{indice}",
                        Fileira = fileira,
                        Indice = indice
                    });
                }
            }

            db.Usuarios.AddRange(
                NovoUsuario(patio.Id, "admin@pklots.local", "Admin@123", "Administrador", Perfis.Admin, hashSenha),
                NovoUsuario(patio.Id, "operator@pklots.local", "Operator@123", "Operador", Perfis.Operador, hashSenha),
                NovoUsuario(patio.Id, "driver@example.com", "Driver@123", "Motorista Exemplo", Perfis.Motorista, hashSenha));

            await db.SaveChangesAsync(ct);
        }

        private static Usuario NovoUsuario(Guid patioId, string email, string senha, string nome, string perfil, IHashSenha hashSenha) =>
            new()
            {
                PatioId = patioId,
                Email = email,
                SenhaHash = hashSenha.Gerar(senha),
                NomeCompleto = nome,
                Perfil = perfil
            };
    }
}
