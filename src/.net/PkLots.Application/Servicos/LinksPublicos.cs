namespace PkLots.Application.Servicos
{
    // Endereços abertos fora do app, como o QR do ticket.
    // A base é o endereço público do gateway (Plataforma:UrlPublica), ex.: https://pklots.com.br
    public class LinksPublicos(string urlBase)
    {
        public const string CaminhoTicket = "/t/";

        public string UrlBase { get; } = urlBase.TrimEnd('/');

        public string Ticket(string token) => UrlBase + CaminhoTicket + token;
    }
}
