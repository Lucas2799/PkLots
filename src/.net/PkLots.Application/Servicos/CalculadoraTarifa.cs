namespace PkLots.Application.Servicos
{
    public record ResultadoTarifa(int DuracaoSegundos, int MinutosCobrados, string DuracaoFormatada, decimal DuracaoHoras, decimal ValorTotal,
        bool Isento = false, string? Detalhamento = null);

    public static class CalculadoraTarifa
    {
        // Cobrança por hora sem tabela (proporcional ao minuto): usada onde só há o valor/hora
        public static ResultadoTarifa Calcular(DateTime entrada, DateTime saida, decimal valorHora, int minutosTolerancia = 0) =>
            Calcular(entrada, saida, new TabelaPreco(new RegrasPreco(ValorHora: valorHora)), minutosTolerancia);

        // A duração é arredondada para cima ao minuto (4h 14m 45s → 4h 15m).
        // Tolerância: quem fica até N minutos não paga; passou disso, cobra o tempo todo.
        // Regras (do dia da entrada: semana ou fim de semana):
        //   hora   → proporcional ao minuto (4,25h × R$ 15 = R$ 63,75)
        //   fracao → primeiro período + cada fração adicional iniciada
        //   pernoite → na janela noturna, cada noite custa no máximo o valor do pernoite
        //   diária máxima → teto a cada 24h iniciadas
        public static ResultadoTarifa Calcular(DateTime entrada, DateTime saida, TabelaPreco tabela, int minutosTolerancia = 0)
        {
            var duracao = saida - entrada;
            if (duracao < TimeSpan.Zero)
                duracao = TimeSpan.Zero;

            var segundos = (int)Math.Floor(duracao.TotalSeconds);
            var minutos = (int)Math.Ceiling(duracao.TotalSeconds / 60d);
            var horas = Math.Round(minutos / 60m, 2);
            var formatada = FormatarMinutos(minutos);
            if (minutosTolerancia > 0 && minutos <= minutosTolerancia)
                return new ResultadoTarifa(segundos, minutos, formatada, horas, 0m, true, $"Dentro da tolerância de {minutosTolerancia} min");

            var regras = tabela.RegrasPara(entrada);
            var detalhes = new List<string>();

            // Noites: o tempo dentro da janela noturna sai do cálculo normal e cada noite custa no máximo o pernoite
            var minutosNoite = 0;
            var valorNoites = 0m;
            if (regras.ValorPernoite is { } pernoite)
            {
                var noitesComPernoite = 0;
                foreach (var minutosNaNoite in MinutosPorNoite(entrada, entrada.AddMinutes(minutos), regras.InicioPernoite, regras.FimPernoite))
                {
                    minutosNoite += minutosNaNoite;
                    var normal = ValorAvulso(regras, minutosNaNoite);
                    if (normal > pernoite)
                    {
                        valorNoites += pernoite;
                        noitesComPernoite++;
                    }
                    else
                        valorNoites += normal;
                }
                if (noitesComPernoite > 0)
                    detalhes.Add($"{noitesComPernoite} pernoite(s) de {Moeda(pernoite)}");
            }

            var minutosDia = minutos - minutosNoite;
            var valorDia = ValorBase(regras, minutosDia, detalhes);
            var total = valorDia + valorNoites;

            if (regras.DiariaMaxima is { } diaria)
            {
                var dias = Math.Max(1, (int)Math.Ceiling(minutos / 1440d));
                var teto = dias * diaria;
                if (total > teto)
                {
                    total = teto;
                    detalhes.Clear();
                    detalhes.Add(dias == 1 ? $"Diária máxima {Moeda(diaria)}" : $"{dias} diárias de {Moeda(diaria)}");
                }
            }

            return new ResultadoTarifa(segundos, minutos, formatada, horas, Math.Round(total, 2, MidpointRounding.AwayFromZero), false,
                detalhes.Count == 0 ? null : string.Join(" + ", detalhes));
        }

        // Tempo fora da janela noturna: primeiro período + frações, ou proporcional à hora
        private static decimal ValorBase(RegrasPreco r, int minutos, List<string> detalhes)
        {
            if (minutos <= 0)
                return 0m;
            if (r.Modo == ModosCobranca.Fracao)
            {
                if (minutos <= r.PrimeiroPeriodoMinutos)
                {
                    detalhes.Add($"{Primeiro(r)} {Moeda(r.ValorPrimeiroPeriodo)}");
                    return r.ValorPrimeiroPeriodo;
                }
                var fracoes = (int)Math.Ceiling((minutos - r.PrimeiroPeriodoMinutos) / (double)r.FracaoMinutos);
                detalhes.Add($"{Primeiro(r)} {Moeda(r.ValorPrimeiroPeriodo)}");
                detalhes.Add($"{fracoes} × {TabelaPreco.Periodo(r.FracaoMinutos)} de {Moeda(r.ValorFracao)}");
                return r.ValorPrimeiroPeriodo + fracoes * r.ValorFracao;
            }
            var valor = Proporcional(minutos, r.ValorHora);
            detalhes.Add($"{FormatarMinutos(minutos)} × {Moeda(r.ValorHora)}/h");
            return valor;
        }

        // Como sempre foi: horas com 4 casas × valor/hora, arredondado ao centavo
        private static decimal Proporcional(int minutos, decimal valorHora) =>
            Math.Round(Math.Round(minutos / 60m, 4) * valorHora, 2, MidpointRounding.AwayFromZero);

        // Quanto custaria um trecho noturno sem o pernoite (frações inteiras ou proporcional)
        private static decimal ValorAvulso(RegrasPreco r, int minutos) =>
            r.Modo == ModosCobranca.Fracao
                ? (int)Math.Ceiling(minutos / (double)r.FracaoMinutos) * r.ValorFracao
                : Proporcional(minutos, r.ValorHora);

        // Minutos da estadia dentro de cada janela noturna (horário de Brasília), uma entrada por noite com sobreposição
        private static IEnumerable<int> MinutosPorNoite(DateTime entradaUtc, DateTime saidaUtc, int inicio, int fim)
        {
            var entrada = Local(entradaUtc);
            var saida = Local(saidaUtc);
            for (var dia = entrada.Date.AddDays(-1); dia <= saida.Date; dia = dia.AddDays(1))
            {
                var abre = dia.AddHours(inicio);
                var fecha = (fim > inicio ? dia : dia.AddDays(1)).AddHours(fim);
                var de = entrada > abre ? entrada : abre;
                var ate = saida < fecha ? saida : fecha;
                var sobreposicao = (int)Math.Round((ate - de).TotalMinutes);
                if (sobreposicao > 0)
                    yield return sobreposicao;
            }
        }

        private static DateTime Local(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), FusoHorario.Brasil);

        private static string Primeiro(RegrasPreco r) =>
            r.PrimeiroPeriodoMinutos == 60 ? "1ª hora" : $"Primeiros {TabelaPreco.Periodo(r.PrimeiroPeriodoMinutos)}";

        private static string Moeda(decimal valor) => Impressao.GeradorTicketEscPos.Moeda(valor);

        public static string FormatarMinutos(int minutos) => $"{minutos / 60}h {minutos % 60}m";

        public static string FormatarDuracao(TimeSpan duracao) =>
            FormatarMinutos((int)Math.Ceiling(Math.Max(0, duracao.TotalSeconds) / 60d));
    }
}
