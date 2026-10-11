namespace PkLots.Application.Servicos
{
    // Endereços abertos fora do app: o QR do ticket e os links enviados por e-mail.
    // A base é o endereço público do gateway (Plataforma:UrlPublica), ex.: https://pklots.com.br
    public class LinksPublicos(string urlBase)
    {
        public const string CaminhoTicket = "/t/";

        public string UrlBase { get; } = urlBase.TrimEnd('/');

        public string Ticket(string token) => UrlBase + CaminhoTicket + token;

        public string Convite(string token) => $"{UrlBase}/?convite={token}";

        public string RedefinirSenha(string token) => $"{UrlBase}/?redefinir={token}";

        public string AtivarConta(string token) => $"{UrlBase}/?ativar={token}";
    }
}
