using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PkLots.Core.Constantes;

namespace PkLots.Api.Endpoints
{
    public static class Autorizacao
    {
        public const string SuperAdmin = "superadmin";
        public const string Admin = "admin";
        public const string Gestao = "gestao"; // admin e operador
        public const string Motorista = "motorista";
        public const string Auditoria = "auditoria"; // admin (a própria empresa) e superadmin (todas)

        public static void AdicionarPoliticas(Microsoft.AspNetCore.Authorization.AuthorizationOptions opcoes)
        {
            opcoes.AddPolicy(SuperAdmin, p => p.RequireRole(Perfis.SuperAdmin));
            opcoes.AddPolicy(Admin, p => p.RequireRole(Perfis.Admin));
            opcoes.AddPolicy(Gestao, p => p.RequireRole(Perfis.Admin, Perfis.Operador));
            opcoes.AddPolicy(Motorista, p => p.RequireRole(Perfis.Motorista));
            opcoes.AddPolicy(Auditoria, p => p.RequireRole(Perfis.Admin, Perfis.SuperAdmin));
        }

        public static Guid ObterUsuarioId(this ClaimsPrincipal usuario)
        {
            var valor = usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? usuario.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(valor, out var id)
                ? id
                : throw new PkLots.Application.Excecoes.ExcecaoNaoAutorizado("Token sem identificação do usuário.");
        }
    }
}
