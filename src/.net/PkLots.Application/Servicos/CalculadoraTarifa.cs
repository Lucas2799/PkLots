namespace PkLots.Application.Servicos
{
    public record ResultadoTarifa(int DuracaoSegundos, int MinutosCobrados, string DuracaoFormatada, decimal DuracaoHoras, decimal ValorTotal);

    public static class CalculadoraTarifa
    {
        // A duração é arredondada para cima ao minuto (4h 14m 45s → 4h 15m)
        // e cobrada proporcionalmente ao valor/hora (4,25h × R$ 15 = R$ 63,75).
        public static ResultadoTarifa Calcular(DateTime entrada, DateTime saida, decimal valorHora)
        {
            var duracao = saida - entrada;
            if (duracao < TimeSpan.Zero)
                duracao = TimeSpan.Zero;

            var segundos = (int)Math.Floor(duracao.TotalSeconds);
            var minutos = (int)Math.Ceiling(duracao.TotalSeconds / 60d);
            var horas = Math.Round(minutos / 60m, 4);
            var total = Math.Round(horas * valorHora, 2, MidpointRounding.AwayFromZero);

            return new ResultadoTarifa(segundos, minutos, FormatarMinutos(minutos), Math.Round(horas, 2), total);
        }

        public static string FormatarMinutos(int minutos) => $"{minutos / 60}h {minutos % 60}m";

        public static string FormatarDuracao(TimeSpan duracao) =>
            FormatarMinutos((int)Math.Ceiling(Math.Max(0, duracao.TotalSeconds) / 60d));
    }
}
