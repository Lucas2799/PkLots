using PkLots.Application.Interfaces;

namespace PkLots.Infrastructure.Seguranca
{
    public class HashSenhaBcrypt : IHashSenha
    {
        public string Gerar(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, workFactor: 11);

        public bool Verificar(string senha, string hash) => BCrypt.Net.BCrypt.Verify(senha, hash);
    }
}
