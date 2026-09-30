using PkLots.Application.DTOs;

namespace PkLots.Application.Interfaces
{
    // Cliente do microsserviço Python de reconhecimento de placas
    public interface IServicoLpr
    {
        Task<ResultadoLprDto> AnalisarAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default);
    }
}
