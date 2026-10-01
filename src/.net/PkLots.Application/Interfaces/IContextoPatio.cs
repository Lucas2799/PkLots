namespace PkLots.Application.Interfaces
{
    // Quem está fazendo a requisição e em qual pátio está trabalhando
    public interface IContextoPatio
    {
        Guid? EmpresaId { get; }
        Guid? UsuarioId { get; }
        string? Perfil { get; }

        // Pátio escolhido no app (cabeçalho X-Patio-Id) ou o primeiro permitido; valida o acesso
        Task<Guid> ObterPatioIdAsync(CancellationToken ct = default);

        // Pátios ativos que o usuário pode usar (admin: todos da empresa; operador: os vinculados)
        Task<IReadOnlyList<Guid>> ObterPatiosPermitidosAsync(CancellationToken ct = default);
    }
}
