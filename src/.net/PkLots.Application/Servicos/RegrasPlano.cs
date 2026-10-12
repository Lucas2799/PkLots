using Microsoft.EntityFrameworkCore;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Limites e recursos do plano da empresa. Sem assinatura (dados antigos, testes) nada é barrado.
    // Baixar de plano não apaga nada: só impede criar além do novo limite.
    public static class RegrasPlano
    {
        private const string FaleComSuporte = "Fale com o suporte do PkLots para mudar de plano.";

        public static Task<Plano?> ObterPlanoAsync(IPkLotsDbContext db, Guid empresaId, CancellationToken ct) =>
            db.Assinaturas.Where(a => a.EmpresaId == empresaId).Select(a => a.Plano).FirstOrDefaultAsync(ct);

        public static async Task<IReadOnlyCollection<string>> RecursosLiberadosAsync(IPkLotsDbContext db, Guid empresaId, CancellationToken ct) =>
            (await ObterPlanoAsync(db, empresaId, ct))?.Recursos ?? (IReadOnlyCollection<string>)RecursosPlano.Todos;

        public static async Task ExigirRecursoAsync(IPkLotsDbContext db, Guid? empresaId, string recurso, CancellationToken ct)
        {
            if (empresaId is null)
                return;
            var plano = await ObterPlanoAsync(db, empresaId.Value, ct);
            if (plano is not null && !plano.Recursos.Contains(recurso))
                throw new ExcecaoProibido($"{RecursosPlano.Nome(recurso)} não está incluído no plano {plano.Nome}. {FaleComSuporte}");
        }

        // Antes de criar ou reativar um pátio
        public static async Task ExigirPatioAdicionalAsync(IPkLotsDbContext db, Guid empresaId, CancellationToken ct)
        {
            if (await ObterPlanoAsync(db, empresaId, ct) is not { MaximoPatios: { } maximo } plano)
                return;
            if (await db.Patios.CountAsync(p => p.EmpresaId == empresaId && p.Ativo, ct) >= maximo)
                throw new ExcecaoProibido($"O plano {plano.Nome} permite até {maximo} pátio(s) ativo(s). {FaleComSuporte}");
        }

        public static async Task ExigirVagasAsync(IPkLotsDbContext db, Guid empresaId, int total, CancellationToken ct)
        {
            if (await ObterPlanoAsync(db, empresaId, ct) is { MaximoVagasPorPatio: { } maximo } plano && total > maximo)
                throw new ExcecaoProibido($"O plano {plano.Nome} permite até {maximo} vagas por pátio. {FaleComSuporte}");
        }

        // Antes de convidar ou reativar um usuário da gestão (admin ou operador)
        public static async Task ExigirUsuarioAdicionalAsync(IPkLotsDbContext db, Guid empresaId, CancellationToken ct)
        {
            if (await ObterPlanoAsync(db, empresaId, ct) is not { MaximoUsuarios: { } maximo } plano)
                return;
            if (await ContarUsuariosAsync(db, empresaId, ct) >= maximo)
                throw new ExcecaoProibido($"O plano {plano.Nome} permite até {maximo} usuário(s) ativo(s). {FaleComSuporte}");
        }

        public static Task<int> ContarUsuariosAsync(IPkLotsDbContext db, Guid empresaId, CancellationToken ct) =>
            db.Usuarios.CountAsync(u => u.EmpresaId == empresaId && u.Ativo && (u.Perfil == Perfis.Admin || u.Perfil == Perfis.Operador), ct);
    }
}
