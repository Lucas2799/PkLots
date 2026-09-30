using PkLots.Application.DTOs;

namespace PkLots.Application.Interfaces
{
    public interface ICacheVagas
    {
        Task<ResumoVagasDto?> ObterAsync(Guid patioId);
        Task DefinirAsync(Guid patioId, ResumoVagasDto resumo);
        Task InvalidarAsync(Guid patioId);
    }
}
