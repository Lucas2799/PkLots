using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;

namespace PkLots.Infrastructure.Servicos
{
    // Usa o pátio do token (claim patio_id); sem token, usa o pátio padrão (MVP single tenant)
    public class ContextoPatio(IHttpContextAccessor httpContext, IPkLotsDbContext db) : IContextoPatio
    {
        public const string ClaimPatio = "patio_id";

        private Guid? _patioId;

        public async Task<Guid> ObterPatioIdAsync(CancellationToken ct = default)
        {
            if (_patioId is not null)
                return _patioId.Value;

            var claim = httpContext.HttpContext?.User.FindFirst(ClaimPatio)?.Value;
            if (Guid.TryParse(claim, out var patioDoToken))
                return (_patioId = patioDoToken).Value;

            var padrao = await db.Patios
                .Where(p => p.Ativo)
                .OrderBy(p => p.CriadoEm)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            return (_patioId = padrao ?? throw new ExcecaoNaoEncontrado("Nenhum pátio ativo cadastrado.")).Value;
        }
    }
}
