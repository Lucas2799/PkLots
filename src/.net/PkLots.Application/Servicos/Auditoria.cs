using PkLots.Application.Interfaces;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    internal static class Auditoria
    {
        public static void Registrar(IPkLotsDbContext db, Guid patioId, string acao, string situacao, string? placa,
            DateTime quando, Dictionary<string, object>? detalhes = null)
        {
            db.LogsAuditoria.Add(new LogAuditoria
            {
                PatioId = patioId,
                Acao = acao,
                Situacao = situacao,
                Placa = placa,
                Detalhes = detalhes ?? new Dictionary<string, object>(),
                CriadoEm = quando
            });
        }
    }
}
