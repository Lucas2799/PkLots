using System.Security.Cryptography;

namespace PkLots.Application.Servicos
{
    // Código do ticket de entrada: 8 caracteres aleatórios, sem caracteres que se confundem (0/O, 1/I).
    // Impresso como "K7M2-Q9XD" e gravado no QR Code como "PKLOTS:K7M2Q9XD".
    public static class CodigoTicket
    {
        public const string Alfabeto = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const int Tamanho = 8;
        public const string PrefixoQrCode = "PKLOTS:";

        public static string Gerar()
        {
            Span<char> codigo = stackalloc char[Tamanho];
            for (var i = 0; i < Tamanho; i++)
                codigo[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
            return new string(codigo);
        }

        public static string Formatar(string codigo) =>
            codigo.Length == Tamanho ? $"{codigo[..4]}-{codigo[4..]}" : codigo;

        public static string ConteudoQrCode(string codigo) => PrefixoQrCode + codigo;

        // Aceita o que vier do leitor ou digitado: "PKLOTS:K7M2Q9XD", "k7m2-q9xd", " K7M2 Q9XD "
        public static string? Normalizar(string? lido)
        {
            if (string.IsNullOrWhiteSpace(lido))
                return null;

            var texto = lido.Trim().ToUpperInvariant();
            if (texto.StartsWith(PrefixoQrCode))
                texto = texto[PrefixoQrCode.Length..];

            var codigo = new string(texto.Where(char.IsLetterOrDigit).ToArray());
            return codigo.Length == Tamanho ? codigo : null;
        }
    }
}
