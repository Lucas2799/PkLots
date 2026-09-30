using PkLots.Application.DTOs;
using PkLots.Core.Entities;

namespace PkLots.Application.Interfaces
{
    public interface IHashSenha
    {
        string Gerar(string senha);
        bool Verificar(string senha, string hash);
    }

    public interface IGeradorToken
    {
        TokenDto Gerar(Usuario usuario);
    }
}
