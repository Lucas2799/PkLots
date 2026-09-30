using System.Text.RegularExpressions;

namespace PkLots.Application.Servicos
{
    public static partial class ValidadorPlaca
    {
        // Padrão antigo (ABC1234) e Mercosul (ABC1D23)
        [GeneratedRegex(@"^[A-Z]{3}\d[A-Z0-9]\d{2}$")]
        private static partial Regex PadraoPlaca();

        public static string? Normalizar(string? placa)
        {
            if (string.IsNullOrWhiteSpace(placa))
                return null;

            var limpa = placa.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            return PadraoPlaca().IsMatch(limpa) ? limpa : null;
        }
    }
}
