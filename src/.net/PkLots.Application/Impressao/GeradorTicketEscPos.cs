using System.Globalization;
using System.Text;
using PkLots.Application.DTOs;
using PkLots.Application.Servicos;

namespace PkLots.Application.Impressao
{
    // Monta os bytes ESC/POS (padrão Epson, aceito pela maioria das térmicas) do ticket de entrada.
    // Acentos são removidos: a tabela de caracteres varia entre modelos e acentos costumam sair errados.
    public static class GeradorTicketEscPos
    {
        private static readonly byte[] Inicializar = [0x1B, 0x40];          // ESC @
        private static readonly byte[] AlinharEsquerda = [0x1B, 0x61, 0x00]; // ESC a 0
        private static readonly byte[] AlinharCentro = [0x1B, 0x61, 0x01];  // ESC a 1
        private static readonly byte[] NegritoLigado = [0x1B, 0x45, 0x01];   // ESC E 1
        private static readonly byte[] NegritoDesligado = [0x1B, 0x45, 0x00];
        private static readonly byte[] TamanhoDuplo = [0x1D, 0x21, 0x11];    // GS ! (largura e altura x2)
        private static readonly byte[] TamanhoNormal = [0x1D, 0x21, 0x00];
        private static readonly byte[] AvancarECortar = [0x1B, 0x64, 0x04, 0x1D, 0x56, 0x42, 0x03]; // ESC d 4 + GS V B (corte parcial)

        public static int Colunas(int larguraPapelMm) => larguraPapelMm <= 58 ? 32 : 48;

        public static byte[] TicketEntrada(TicketEntradaDto ticket)
        {
            var colunas = Colunas(ticket.LarguraPapelMm);
            var entradaLocal = TimeZoneInfo.ConvertTimeFromUtc(ticket.DataHoraEntrada, FusoHorario.Brasil);
            var b = new Construtor();

            b.Bytes(Inicializar, AlinharCentro, NegritoLigado, TamanhoDuplo).Linha("PKLOTS")
             .Bytes(TamanhoNormal).Linha(ticket.NomePatio).Bytes(NegritoDesligado)
             .Linha("TICKET DE ENTRADA")
             .Linha(new string('-', colunas))
             .Bytes(NegritoLigado, TamanhoDuplo).Linha(FormatarPlaca(ticket.Placa));
            if (ticket.NumeroVaga is not null)
                b.Linha($"VAGA {ticket.NumeroVaga}");
            b.Bytes(TamanhoNormal, NegritoDesligado)
             .Linha(new string('-', colunas))
             .Bytes(AlinharEsquerda)
             .Linha(Coluna("Entrada", entradaLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), colunas))
             .Linha(Coluna("Valor por hora", Moeda(ticket.ValorHora), colunas));
            if (ticket.MinutosTolerancia > 0)
                b.Linha(Coluna("Tolerancia gratuita", $"{ticket.MinutosTolerancia} min", colunas));
            b.Linha(new string('-', colunas))
             .Bytes(AlinharCentro)
             .QrCode(ticket.ConteudoQrCode, ticket.LarguraPapelMm <= 58 ? (byte)5 : (byte)6)
             .Linha("Acompanhe pelo celular: leia o QR")
             .Bytes(NegritoLigado).Linha($"Codigo: {ticket.Codigo}").Bytes(NegritoDesligado)
             .Linha("Guarde este ticket ate a saida")
             .Bytes(AvancarECortar);

            return b.Gerar();
        }

        public static byte[] Teste(int larguraPapelMm, DateTime agoraUtc)
        {
            var colunas = Colunas(larguraPapelMm);
            var agora = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, FusoHorario.Brasil);
            return new Construtor()
                .Bytes(Inicializar, AlinharCentro, NegritoLigado, TamanhoDuplo).Linha("PKLOTS")
                .Bytes(TamanhoNormal).Linha("Impressao de teste").Bytes(NegritoDesligado)
                .Linha(new string('-', colunas))
                .Linha($"Papel: {larguraPapelMm} mm ({colunas} colunas)")
                .Linha(agora.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture))
                .Linha(new string('=', colunas))
                .QrCode(CodigoTicket.ConteudoQrCode("TESTE234"), 5)
                .Bytes(AvancarECortar)
                .Gerar();
        }

        // "ABC1234" → "ABC-1234"; Mercosul fica como está
        public static string FormatarPlaca(string placa) =>
            placa.Length == 7 && char.IsDigit(placa[4]) ? $"{placa[..3]}-{placa[3..]}" : placa;

        public static string Moeda(decimal valor) =>
            "R$ " + valor.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', '_').Replace('.', ',').Replace('_', '.');

        // "Entrada.............01/10 10:30" com o valor alinhado à direita
        private static string Coluna(string rotulo, string valor, int colunas)
        {
            var espacos = Math.Max(1, colunas - rotulo.Length - valor.Length);
            return rotulo + new string(' ', espacos) + valor;
        }

        public static string RemoverAcentos(string texto)
        {
            const string com = "áàâãäéèêëíìîïóòôõöúùûüçñÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑ";
            const string sem = "aaaaaeeeeiiiiooooouuuucnAAAAAEEEEIIIIOOOOOUUUUCN";
            var resultado = new StringBuilder(texto.Length);
            foreach (var c in texto)
            {
                var i = com.IndexOf(c);
                var trocado = i >= 0 ? sem[i] : c;
                resultado.Append(trocado is >= ' ' and <= '~' ? trocado : '?');
            }
            return resultado.ToString();
        }

        private sealed class Construtor
        {
            private readonly List<byte> _bytes = [];

            public Construtor Bytes(params byte[][] comandos)
            {
                foreach (var comando in comandos)
                    _bytes.AddRange(comando);
                return this;
            }

            public Construtor Linha(string texto)
            {
                _bytes.AddRange(Encoding.ASCII.GetBytes(RemoverAcentos(texto)));
                _bytes.Add(0x0A);
                return this;
            }

            // QR Code nativo da impressora (GS ( k), modelo 2, correção de erro M
            public Construtor QrCode(string conteudo, byte tamanhoModulo)
            {
                var dados = Encoding.ASCII.GetBytes(RemoverAcentos(conteudo));
                var tamanho = dados.Length + 3;
                Bytes(
                    [0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00],          // modelo 2
                    [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, tamanhoModulo],      // tamanho do módulo
                    [0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31],               // correção M
                    [0x1D, 0x28, 0x6B, (byte)(tamanho % 256), (byte)(tamanho / 256), 0x31, 0x50, 0x30]);
                _bytes.AddRange(dados);
                Bytes([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30]);           // imprimir
                _bytes.Add(0x0A);
                return this;
            }

            public byte[] Gerar() => [.. _bytes];
        }
    }
}
