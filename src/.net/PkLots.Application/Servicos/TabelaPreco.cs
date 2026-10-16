using System.Text.Json;
using PkLots.Application.Excecoes;
using PkLots.Application.Impressao;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Regras de cobrança de um conjunto de dias (semana ou fim de semana)
    public record RegrasPreco(
        string Modo = ModosCobranca.Hora,     // hora: proporcional ao minuto; fracao: primeiro período + frações
        decimal ValorHora = 15m,
        int PrimeiroPeriodoMinutos = 60,
        decimal ValorPrimeiroPeriodo = 0m,
        int FracaoMinutos = 60,               // cada fração iniciada é cobrada inteira
        decimal ValorFracao = 0m,
        decimal? DiariaMaxima = null,         // teto a cada 24h iniciadas
        decimal? ValorPernoite = null,        // máximo por noite dentro da janela noturna
        int InicioPernoite = 20,              // horas locais (0–23)
        int FimPernoite = 8);

    // Tabela de preço do pátio. FimDeSemana nulo: sábado e domingo seguem a semana.
    public record TabelaPreco(RegrasPreco Semana, RegrasPreco? FimDeSemana = null)
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        // Pátio sem tabela salva: cobrança por hora com o valor/hora do pátio (comportamento original)
        public static TabelaPreco Do(Patio patio) => De(patio.TabelaPrecoJson, patio.ValorHora);

        // O valor/hora da semana vem sempre do pátio (uma só fonte, usada também nas telas antigas)
        public static TabelaPreco De(string? json, decimal valorHora)
        {
            var tabela = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TabelaPreco>(json, Json);
            tabela ??= new TabelaPreco(new RegrasPreco());
            return tabela with { Semana = tabela.Semana with { ValorHora = valorHora } };
        }

        public string ParaJson() => JsonSerializer.Serialize(this, Json);

        // Sábado e domingo pelo dia da entrada (horário de Brasília)
        public RegrasPreco RegrasPara(DateTime entradaUtc)
        {
            var dia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entradaUtc, DateTimeKind.Utc), FusoHorario.Brasil).DayOfWeek;
            return dia is DayOfWeek.Saturday or DayOfWeek.Sunday && FimDeSemana is not null ? FimDeSemana : Semana;
        }

        public void Validar()
        {
            ValidarRegras(Semana, "Semana");
            if (FimDeSemana is not null)
                ValidarRegras(FimDeSemana, "Fim de semana");
        }

        private static void ValidarRegras(RegrasPreco r, string nome)
        {
            void Valor(decimal valor, string campo, bool permitirZero = false)
            {
                if (valor < 0 || (!permitirZero && valor == 0) || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
                    throw new ExcecaoNegocio($"{nome}: {campo} inválido (até R$ 1.000,00, com duas casas decimais).");
            }
            void Minutos(int minutos, string campo)
            {
                if (minutos is < 1 or > 1440)
                    throw new ExcecaoNegocio($"{nome}: {campo} deve ficar entre 1 e 1440 minutos.");
            }

            switch (r.Modo)
            {
                case ModosCobranca.Hora:
                    Valor(r.ValorHora, "valor por hora");
                    break;
                case ModosCobranca.Fracao:
                    Minutos(r.PrimeiroPeriodoMinutos, "o primeiro período");
                    Minutos(r.FracaoMinutos, "a fração adicional");
                    Valor(r.ValorPrimeiroPeriodo, "valor do primeiro período", permitirZero: true);
                    Valor(r.ValorFracao, "valor da fração adicional", permitirZero: true);
                    if (r.ValorPrimeiroPeriodo == 0 && r.ValorFracao == 0)
                        throw new ExcecaoNegocio($"{nome}: informe o valor do primeiro período ou da fração adicional.");
                    break;
                default:
                    throw new ExcecaoNegocio($"{nome}: modo de cobrança inválido. Use hora ou fracao.");
            }
            if (r.DiariaMaxima is { } diaria)
                Valor(diaria, "diária máxima");
            if (r.ValorPernoite is { } pernoite)
            {
                Valor(pernoite, "valor do pernoite");
                if (r.InicioPernoite is < 0 or > 23 || r.FimPernoite is < 0 or > 23 || r.InicioPernoite == r.FimPernoite)
                    throw new ExcecaoNegocio($"{nome}: o horário do pernoite deve ter início e fim diferentes (0 a 23h).");
            }
        }

        public const decimal ValorMaximo = 1000m;

        // Texto curto para o ticket e a página do QR: "1ª hora R$ 10,00 · a cada 30 min R$ 3,00 · diária máx. R$ 50,00"
        public string Descricao()
        {
            var semana = Descrever(Semana);
            return FimDeSemana is null ? semana : $"Seg. a sex.: {semana}. Sáb. e dom.: {Descrever(FimDeSemana)}";
        }

        public static string Descrever(RegrasPreco r)
        {
            var partes = new List<string>();
            if (r.Modo == ModosCobranca.Hora)
                partes.Add($"{Moeda(r.ValorHora)} por hora");
            else
            {
                partes.Add($"{(r.PrimeiroPeriodoMinutos == 60 ? "1ª hora" : $"primeiros {Periodo(r.PrimeiroPeriodoMinutos)}")} {Moeda(r.ValorPrimeiroPeriodo)}");
                partes.Add($"{(r.FracaoMinutos == 60 ? "hora adicional ou fração" : $"cada {Periodo(r.FracaoMinutos)} adicionais ou fração")} {Moeda(r.ValorFracao)}");
            }
            if (r.DiariaMaxima is { } diaria)
                partes.Add($"diária máx. {Moeda(diaria)}");
            if (r.ValorPernoite is { } pernoite)
                partes.Add($"pernoite ({r.InicioPernoite}h às {r.FimPernoite}h) até {Moeda(pernoite)}");
            return string.Join(" · ", partes);
        }

        public static string Periodo(int minutos) =>
            minutos % 60 == 0 ? $"{minutos / 60}h" : minutos > 60 ? $"{minutos / 60}h{minutos % 60:00}" : $"{minutos} min";

        private static string Moeda(decimal valor) => GeradorTicketEscPos.Moeda(valor);
    }

    public static class ModosCobranca
    {
        public const string Hora = "hora";
        public const string Fracao = "fracao";
    }
}
