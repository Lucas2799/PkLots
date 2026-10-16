using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;

namespace PkLots.Api.Middleware
{
    // Converte exceções da aplicação em respostas ProblemDetails padronizadas
    public class ManipuladorExcecoes(ILogger<ManipuladorExcecoes> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
        {
            var (status, titulo) = excecao switch
            {
                ExcecaoNegocio => (StatusCodes.Status400BadRequest, "Requisição inválida"),
                ExcecaoNaoAutorizado => (StatusCodes.Status401Unauthorized, "Não autorizado"),
                ExcecaoProibido => (StatusCodes.Status403Forbidden, "Acesso negado"),
                ExcecaoNaoEncontrado => (StatusCodes.Status404NotFound, "Não encontrado"),
                ExcecaoConflito => (StatusCodes.Status409Conflict, "Conflito"),
                _ when ErrosBanco.Concorrencia(excecao) => (StatusCodes.Status409Conflict, "Conflito"),
                ExcecaoReconhecimento => (StatusCodes.Status422UnprocessableEntity, "Placa não reconhecida"),
                ExcecaoServicoExterno => (StatusCodes.Status503ServiceUnavailable, "Serviço indisponível"),
                _ => (StatusCodes.Status500InternalServerError, "Erro interno")
            };

            if (status == StatusCodes.Status503ServiceUnavailable)
                logger.LogWarning(excecao.InnerException, "{Mensagem} ({Metodo} {Caminho})", excecao.Message, contexto.Request.Method, contexto.Request.Path);
            else if (status >= 500)
                logger.LogError(excecao, "Erro ao processar {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);

            var detalhe = status == StatusCodes.Status500InternalServerError
                ? "Ocorreu um erro inesperado. Tente novamente."
                : excecao is ExcecaoConflito or not DbUpdateException
                    ? excecao.Message
                    // Gravação simultânea sem tratamento próprio no serviço: nunca expõe o erro do banco
                    : "Outra pessoa alterou este registro ao mesmo tempo. Atualize a tela e tente novamente.";

            contexto.Response.StatusCode = status;
            await contexto.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalhe,
                Instance = contexto.Request.Path
            }, ct);

            return true;
        }
    }
}
