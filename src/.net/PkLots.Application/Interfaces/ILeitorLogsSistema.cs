using PkLots.Application.DTOs;

namespace PkLots.Application.Interfaces
{
    // Log técnico da API (requisições, avisos e erros), gravado em arquivo pelo Serilog
    public interface ILeitorLogsSistema
    {
        Task<PaginaLogsSistemaDto> LerAsync(FiltroLogsSistema filtro, CancellationToken ct = default);
    }
}
