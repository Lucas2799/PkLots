using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PkLots.Application.DTOs;
using PkLots.Application.Interfaces;
using PkLots.Core.Entities;
using PkLots.Infrastructure.Servicos;

namespace PkLots.Infrastructure.Seguranca
{
    public class OpcoesJwt
    {
        public string SecretKey { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 1440;
        public string Issuer { get; set; } = "pklots-api";
        public string Audience { get; set; } = "pklots-app";
    }

    public class GeradorTokenJwt(IOptions<OpcoesJwt> opcoes, TimeProvider relogio) : IGeradorToken
    {
        public TokenDto Gerar(Usuario usuario)
        {
            var config = opcoes.Value;
            var agora = relogio.GetUtcNow().UtcDateTime;
            var expiraEm = agora.AddMinutes(config.ExpirationMinutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(JwtRegisteredClaimNames.Name, usuario.NomeCompleto),
                new Claim(ClaimTypes.Role, usuario.Perfil),
                new Claim(ContextoPatio.ClaimPatio, usuario.PatioId.ToString())
            };

            var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SecretKey));
            var token = new JwtSecurityToken(
                issuer: config.Issuer,
                audience: config.Audience,
                claims: claims,
                notBefore: agora,
                expires: expiraEm,
                signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));

            return new TokenDto(new JwtSecurityTokenHandler().WriteToken(token), config.ExpirationMinutes * 60, usuario.Perfil);
        }
    }
}
