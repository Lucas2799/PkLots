using System.Net.Sockets;
using PkLots.Application.Interfaces;

namespace PkLots.Infrastructure.Servicos
{
    // Impressão "RAW" na porta 9100 (JetDirect), suportada pelas térmicas de rede ESC/POS
    public class ImpressoraTicketsTcp : IImpressoraTickets
    {
        private static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(4);

        public async Task EnviarAsync(string host, int porta, byte[] dados, CancellationToken ct = default)
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TempoLimite);

            using var cliente = new TcpClient { NoDelay = true };
            await cliente.ConnectAsync(host, porta, limite.Token);
            await using var fluxo = cliente.GetStream();
            await fluxo.WriteAsync(dados, limite.Token);
            await fluxo.FlushAsync(limite.Token);
        }
    }
}
