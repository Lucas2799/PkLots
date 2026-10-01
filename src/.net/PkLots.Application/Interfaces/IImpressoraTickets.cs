namespace PkLots.Application.Interfaces
{
    // Envio de bytes ESC/POS para uma impressora térmica de rede (TCP, normalmente porta 9100)
    public interface IImpressoraTickets
    {
        Task EnviarAsync(string host, int porta, byte[] dados, CancellationToken ct = default);
    }
}
