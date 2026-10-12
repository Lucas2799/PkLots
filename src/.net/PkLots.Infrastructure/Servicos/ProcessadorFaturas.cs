using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PkLots.Application.Servicos;

namespace PkLots.Infrastructure.Servicos
{
    // Emite as mensalidades do PkLots e envia os avisos de cobrança em segundo plano (rodar de novo não duplica)
    public class ProcessadorFaturas(
        IServiceScopeFactory escopos,
        TimeProvider relogio,
        ILogger<ProcessadorFaturas> logger) : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);
        private static readonly TimeSpan Espera = TimeSpan.FromMinutes(1); // deixa as migrations terminarem

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(Espera, relogio, ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var escopo = escopos.CreateScope();
                    var faturas = escopo.ServiceProvider.GetRequiredService<ServicoFaturas>();
                    var resultado = await faturas.EmitirMensalidadesAsync(ct);
                    if (resultado.Emitidas > 0)
                        logger.LogInformation("{Quantidade} fatura(s) do PkLots emitida(s)", resultado.Emitidas);
                    var avisos = await faturas.EnviarAvisosAsync(ct);
                    if (avisos > 0)
                        logger.LogInformation("{Quantidade} aviso(s) de cobrança enviado(s)", avisos);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Falha ao emitir as faturas do PkLots");
                }
                await Task.Delay(Intervalo, relogio, ct);
            }
        }
    }
}
