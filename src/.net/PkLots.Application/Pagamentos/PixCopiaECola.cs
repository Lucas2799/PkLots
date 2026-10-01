using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PkLots.Application.Pagamentos
{
    // PIX "copia e cola" (BR Code, padrão EMV QRCPS do Banco Central) com chave e valor definidos.
    // Gerado localmente: o dinheiro vai direto para a chave do pátio, sem intermediário.
    public static partial class PixCopiaECola
    {
        public static string Gerar(string chave, string nomeRecebedor, string cidadeRecebedor, decimal valor, string txId)
        {
            var contaRecebedor = Campo("00", "br.gov.bcb.pix") + Campo("01", chave.Trim());

            var conteudo = new StringBuilder()
                .Append(Campo("00", "01"))                                   // versão do payload
                .Append(Campo("26", contaRecebedor))                         // conta PIX do recebedor
                .Append(Campo("52", "0000"))                                 // categoria (não informada)
                .Append(Campo("53", "986"))                                  // moeda: real
                .Append(Campo("54", valor.ToString("0.00", CultureInfo.InvariantCulture)))
                .Append(Campo("58", "BR"))
                .Append(Campo("59", Normalizar(nomeRecebedor, 25)))
                .Append(Campo("60", Normalizar(cidadeRecebedor, 15)))
                .Append(Campo("62", Campo("05", NormalizarTxId(txId))))      // identificador da cobrança
                .Append("6304")
                .ToString();

            return conteudo + Crc16(conteudo).ToString("X4");
        }

        // CRC16-CCITT (polinômio 0x1021, inicial 0xFFFF), exigido no campo 63
        public static ushort Crc16(string texto)
        {
            ushort crc = 0xFFFF;
            foreach (var b in Encoding.UTF8.GetBytes(texto))
            {
                crc ^= (ushort)(b << 8);
                for (var i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }

        // Tipos de chave aceitos: CPF, CNPJ, e-mail, telefone (+55...) e chave aleatória
        public static string? ValidarChave(string? chave)
        {
            if (string.IsNullOrWhiteSpace(chave))
                return null;
            var c = chave.Trim();

            if (ChaveAleatoria().IsMatch(c)) return c.ToLowerInvariant();
            if (c.Contains('@')) return Email().IsMatch(c) && c.Length <= 77 ? c.ToLowerInvariant() : null;
            if (c.StartsWith('+')) return Telefone().IsMatch(c) ? c : null;

            var digitos = new string(c.Where(char.IsDigit).ToArray());
            if (!c.All(ch => char.IsDigit(ch) || ch is '.' or '-' or '/'))
                return null;
            return (digitos.Length == 11 && CpfValido(digitos)) || (digitos.Length == 14 && CnpjValido(digitos)) ? digitos : null;
        }

        // Dígitos verificadores (evita, por exemplo, telefone sem +55 salvo como "CPF")
        public static bool CpfValido(string cpf)
        {
            if (cpf.Length != 11 || cpf.Distinct().Count() == 1) return false;
            int Digito(int tamanho)
            {
                var soma = 0;
                for (var i = 0; i < tamanho; i++) soma += (cpf[i] - '0') * (tamanho + 1 - i);
                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }
            return Digito(9) == cpf[9] - '0' && Digito(10) == cpf[10] - '0';
        }

        public static bool CnpjValido(string cnpj)
        {
            if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1) return false;
            int Digito(int tamanho)
            {
                int[] pesos = tamanho == 12 ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2] : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
                var soma = 0;
                for (var i = 0; i < tamanho; i++) soma += (cnpj[i] - '0') * pesos[i];
                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }
            return Digito(12) == cnpj[12] - '0' && Digito(13) == cnpj[13] - '0';
        }

        private static string Campo(string id, string valor) => $"{id}{valor.Length:00}{valor}";

        // Nome e cidade: sem acentos, maiúsculas, tamanho máximo do padrão
        private static string Normalizar(string texto, int maximo)
        {
            var semAcento = Impressao.GeradorTicketEscPos.RemoverAcentos(texto.Trim()).ToUpperInvariant();
            var limpo = new string(semAcento.Where(c => c is >= ' ' and <= '~').ToArray());
            return limpo.Length > maximo ? limpo[..maximo].TrimEnd() : limpo;
        }

        private static string NormalizarTxId(string txId)
        {
            var limpo = new string(txId.Where(char.IsAsciiLetterOrDigit).ToArray());
            return limpo.Length == 0 ? "***" : limpo.Length > 25 ? limpo[..25] : limpo;
        }

        [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
        private static partial Regex ChaveAleatoria();

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex Email();

        [GeneratedRegex(@"^\+55\d{10,11}$")]
        private static partial Regex Telefone();
    }
}
