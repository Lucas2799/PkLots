namespace PkLots.Application.Interfaces
{
    // Resolve o pátio da requisição atual (claim do token ou pátio padrão no MVP)
    public interface IContextoPatio
    {
        Task<Guid> ObterPatioIdAsync(CancellationToken ct = default);
    }
}
