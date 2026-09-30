namespace PkLots.Application.Excecoes
{
    // Violação de regra de negócio (HTTP 400)
    public class ExcecaoNegocio(string mensagem) : Exception(mensagem);

    // Conflito de estado, ex.: veículo já estacionado, pátio lotado (HTTP 409)
    public class ExcecaoConflito(string mensagem) : Exception(mensagem);

    // Recurso inexistente (HTTP 404)
    public class ExcecaoNaoEncontrado(string mensagem) : Exception(mensagem);

    // Credenciais inválidas (HTTP 401)
    public class ExcecaoNaoAutorizado(string mensagem) : Exception(mensagem);

    // Falha no reconhecimento de placa (HTTP 422)
    public class ExcecaoReconhecimento(string mensagem) : Exception(mensagem);

    // Serviço externo (ex.: LPR) fora do ar ou respondendo com erro (HTTP 503)
    public class ExcecaoServicoExterno(string mensagem, Exception? interna = null) : Exception(mensagem, interna);
}
