using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;

namespace PkLots.Infrastructure.Servicos
{
    // Cliente HTTP do microsserviço Python (POST /v1/lpr/analisar)
    public class ServicoLprHttp(HttpClient http) : IServicoLpr
    {
        public async Task<ResultadoLprDto> AnalisarAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default)
        {
            using var conteudo = new MultipartFormDataContent();
            var arquivo = new StreamContent(imagem);
            arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            conteudo.Add(arquivo, "imagem", nomeArquivo);

            try
            {
                using var resposta = await http.PostAsync("v1/lpr/analisar", conteudo, ct);
                if (!resposta.IsSuccessStatusCode)
                    throw new ExcecaoServicoExterno($"Serviço de reconhecimento respondeu {(int)resposta.StatusCode}.");

                var corpo = await resposta.Content.ReadFromJsonAsync<RespostaLpr>(cancellationToken: ct)
                    ?? throw new ExcecaoServicoExterno("Resposta vazia do serviço de reconhecimento.");

                return new ResultadoLprDto(corpo.Sucesso, corpo.Placa, corpo.Confianca, corpo.Metodo ?? "easyocr");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new ExcecaoServicoExterno("Serviço de reconhecimento de placas indisponível.", ex);
            }
        }

        private record RespostaLpr(
            [property: JsonPropertyName("sucesso")] bool Sucesso,
            [property: JsonPropertyName("placa")] string? Placa,
            [property: JsonPropertyName("confianca")] decimal Confianca,
            [property: JsonPropertyName("metodo")] string? Metodo);
    }
}
