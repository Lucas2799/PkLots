using System.Globalization;
using System.Text.Json;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;

namespace PkLots.Infrastructure.Servicos
{
    // Lê os arquivos JSON do Serilog (um por dia, logs/json/pklots-AAAAMMDD.json), do mais recente para o mais antigo.
    // Cada linha é um evento no formato "compact" (@t data, @l nível, @m mensagem, @x exceção e as propriedades).
    public class LeitorLogsArquivo(string pasta, TimeProvider relogio) : ILeitorLogsSistema
    {
        public const string PastaPadrao = "logs/json";
        public const string Prefixo = "pklots-";
        public const int DiasMaximos = 7;
        public const int TamanhoMaximoPagina = 500;

        private static readonly string[] Niveis = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

        public async Task<PaginaLogsSistemaDto> LerAsync(FiltroLogsSistema filtro, CancellationToken ct = default)
        {
            var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(relogio.GetUtcNow().UtcDateTime, FusoHorario.Brasil));
            var ate = filtro.Ate ?? hoje;
            var de = filtro.De ?? ate;
            if (de > ate)
                throw new ExcecaoNegocio("A data inicial deve ser antes da final.");
            if (ate.DayNumber - de.DayNumber + 1 > DiasMaximos)
                throw new ExcecaoNegocio($"Consulte até {DiasMaximos} dias de log por vez.");

            var minimo = Array.FindIndex(Niveis, n => n.Equals(filtro.Nivel ?? "Information", StringComparison.OrdinalIgnoreCase));
            if (minimo < 0)
                throw new ExcecaoNegocio("Nível inválido. Use Information, Warning ou Error.");
            var texto = string.IsNullOrWhiteSpace(filtro.Texto) ? null : filtro.Texto.Trim();
            var pagina = Math.Max(1, filtro.Pagina);
            var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, TamanhoMaximoPagina);

            var inicio = TimeZoneInfo.ConvertTimeToUtc(de.ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil);
            var fim = TimeZoneInfo.ConvertTimeToUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil);

            if (!Directory.Exists(pasta))
                return new PaginaLogsSistemaDto([], 0, pagina, tamanho, de, ate, false);

            // O nome do arquivo usa a data do servidor (UTC no container): lê um dia a mais de cada lado e filtra pela hora
            var eventos = new List<RegistroLogSistemaDto>();
            for (var dia = DateOnly.FromDateTime(inicio).AddDays(-1); dia <= DateOnly.FromDateTime(fim); dia = dia.AddDays(1))
            {
                var arquivo = Path.Combine(pasta, $"{Prefixo}{dia:yyyyMMdd}.json");
                if (!File.Exists(arquivo))
                    continue;
                foreach (var linha in await LerLinhasAsync(arquivo, ct))
                    if (Interpretar(linha) is { } e && e.Quando >= inicio && e.Quando < fim
                        && Array.IndexOf(Niveis, e.Nivel) >= minimo && Contem(e, texto))
                        eventos.Add(e);
            }

            var ordenados = eventos.OrderByDescending(e => e.Quando).ToList();
            return new PaginaLogsSistemaDto(ordenados.Skip((pagina - 1) * tamanho).Take(tamanho).ToList(), ordenados.Count,
                pagina, tamanho, de, ate, true);
        }

        // O Serilog mantém o arquivo do dia aberto: lê com compartilhamento de escrita
        private static async Task<List<string>> LerLinhasAsync(string arquivo, CancellationToken ct)
        {
            await using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var leitor = new StreamReader(fluxo);
            var linhas = new List<string>();
            while (await leitor.ReadLineAsync(ct) is { } linha)
                linhas.Add(linha);
            return linhas;
        }

        private static bool Contem(RegistroLogSistemaDto e, string? texto) =>
            texto is null
            || e.Mensagem.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || (e.Excecao?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
            || (e.Caminho?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
            || (e.Origem?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false);

        // Linha inválida (ex.: cortada no meio da gravação) é ignorada
        public static RegistroLogSistemaDto? Interpretar(string linha)
        {
            if (string.IsNullOrWhiteSpace(linha))
                return null;
            try
            {
                using var json = JsonDocument.Parse(linha);
                var raiz = json.RootElement;
                if (!raiz.TryGetProperty("@t", out var t) || !DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando))
                    return null;
                string? Texto(string nome) => raiz.TryGetProperty(nome, out var p) && p.ValueKind != JsonValueKind.Null
                    ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString())
                    : null;
                int? status = raiz.TryGetProperty("StatusCode", out var s) && s.TryGetInt32(out var codigo) ? codigo : null;

                return new RegistroLogSistemaDto(
                    DateTime.SpecifyKind(quando, DateTimeKind.Utc),
                    Texto("@l") ?? "Information", // o formato "compact" omite o nível Information
                    Texto("@m") ?? Texto("@mt") ?? "",
                    Texto("@x"),
                    Texto("SourceContext"),
                    Texto("RequestPath"),
                    status);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
