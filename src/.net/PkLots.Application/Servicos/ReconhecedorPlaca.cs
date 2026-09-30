using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Envia a foto ao LPR e registra auditoria quando a placa não é reconhecida
    public class ReconhecedorPlaca(
        IServicoLpr lpr,
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        TimeProvider relogio)
    {
        public async Task<ResultadoLprDto> ReconhecerAsync(Stream imagem, string nomeArquivo, string origem, CancellationToken ct = default)
        {
            var resultado = await lpr.AnalisarAsync(imagem, nomeArquivo, ct);
            var placa = ValidadorPlaca.Normalizar(resultado.Placa);

            if (resultado.Sucesso && placa is not null)
                return resultado with { Placa = placa };

            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            Auditoria.Registrar(db, patioId, AcoesAuditoria.LprFalhou, SituacaoAuditoria.Erro, resultado.Placa,
                relogio.GetUtcNow().UtcDateTime,
                new()
                {
                    ["origem"] = origem,
                    ["confianca"] = resultado.Confianca,
                    ["metodo"] = resultado.Metodo
                });
            await db.SaveChangesAsync(ct);

            throw new ExcecaoReconhecimento("Não foi possível reconhecer a placa na imagem.");
        }
    }
}
