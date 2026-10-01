using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Infrastructure.Servicos
{
    // Empresa e perfil vêm do token; o pátio vem do cabeçalho X-Patio-Id (seletor do app) e é validado
    public class ContextoPatio(IHttpContextAccessor httpContext, IPkLotsDbContext db) : IContextoPatio
    {
        public const string ClaimEmpresa = Data.PkLotsDbContext.ClaimEmpresa;
        public const string CabecalhoPatio = "X-Patio-Id";

        private Guid? _patioId;
        private IReadOnlyList<Guid>? _permitidos;

        private ClaimsPrincipal? Usuario => httpContext.HttpContext?.User;

        public Guid? EmpresaId => Guid.TryParse(Usuario?.FindFirstValue(ClaimEmpresa), out var id) ? id : null;

        public Guid? UsuarioId =>
            Guid.TryParse(Usuario?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Usuario?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
                ? id
                : null;

        public string? Perfil => Usuario?.FindFirstValue(ClaimTypes.Role);

        public async Task<Guid> ObterPatioIdAsync(CancellationToken ct = default)
        {
            if (_patioId is not null)
                return _patioId.Value;

            var permitidos = await ObterPatiosPermitidosAsync(ct);
            var cabecalho = httpContext.HttpContext?.Request.Headers[CabecalhoPatio].FirstOrDefault();

            if (Guid.TryParse(cabecalho, out var escolhido))
            {
                if (!permitidos.Contains(escolhido))
                    throw new ExcecaoProibido("Você não tem acesso a este pátio.");
                return (_patioId = escolhido).Value;
            }

            if (permitidos.Count == 0)
                throw new ExcecaoNaoEncontrado("Nenhum pátio disponível para este usuário.");
            return (_patioId = permitidos[0]).Value;
        }

        public async Task<IReadOnlyList<Guid>> ObterPatiosPermitidosAsync(CancellationToken ct = default)
        {
            if (_permitidos is not null)
                return _permitidos;

            var empresaId = EmpresaId;
            if (empresaId is null)
                return _permitidos = [];

            var consulta = db.Patios.Where(p => p.EmpresaId == empresaId && p.Ativo);
            if (Perfil == Perfis.Operador)
            {
                var usuarioId = UsuarioId;
                consulta = consulta.Where(p => db.UsuariosPatios.Any(up => up.UsuarioId == usuarioId && up.PatioId == p.Id));
            }

            return _permitidos = await consulta.OrderBy(p => p.CriadoEm).ThenBy(p => p.Nome).Select(p => p.Id).ToListAsync(ct);
        }
    }
}
