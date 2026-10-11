using System.Security.Cryptography;
using System.Text;

namespace PkLots.Application.Servicos
{
    // Segredos aleatórios para links (ticket, convite, redefinição de senha), seguros para URL (base64url)
    public static class TokenSeguro
    {
        public const int BytesTicket = 16; // 128 bits: 22 caracteres no link do QR
        public const int BytesEmail = 32;  // 256 bits: links que dão acesso a uma conta

        public static string Gerar(int bytes) =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        // Formato esperado antes de consultar o banco (descarta lixo e tentativas óbvias sem custo)
        public static bool FormatoValido(string? token, int bytes) =>
            token is not null
            && token.Length == (bytes * 4 + 2) / 3
            && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }
}
