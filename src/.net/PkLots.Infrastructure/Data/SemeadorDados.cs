using Microsoft.EntityFrameworkCore;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    // Dados iniciais do MVP: pátio padrão com 30 vagas (A1–C10) e os três usuários padrão
    public static class SemeadorDados
    {
        private const int TotalVagasInicial = 30;

        public static async Task SemearAsync(PkLotsDbContext db, IHashSenha hashSenha, CancellationToken ct = default)
        {
            if (await db.Patios.AnyAsync(ct))
                return;

            var patio = new Patio
            {
                Nome = "Pátio Padrão",
                Descricao = "Pátio inicial do MVP",
                MaximoVagas = TotalVagasInicial,
                ValorHora = 15.00m
            };
            db.Patios.Add(patio);

            for (var posicao = 0; posicao < TotalVagasInicial; posicao++)
            {
                db.Vagas.Add(new Vaga
                {
                    PatioId = patio.Id,
                    Numero = LayoutPatio.Numero(posicao),
                    Fileira = LayoutPatio.Fileira(posicao),
                    Indice = LayoutPatio.Indice(posicao)
                });
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
