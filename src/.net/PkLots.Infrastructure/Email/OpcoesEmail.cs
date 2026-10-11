namespace PkLots.Infrastructure.Email
{
    // Servidor SMTP (seção "Email"). Sem Host, os e-mails ficam na fila até o SMTP ser configurado.
    // Desenvolvimento: smtp4dev (perfil "dev" do compose). Produção: qualquer SMTP (Brevo, SES, Resend...).
    public class OpcoesEmail
    {
        public string? Host { get; set; }
        public int Porta { get; set; } = 25;
        public string? Usuario { get; set; }
        public string? Senha { get; set; }
        public string Remetente { get; set; } = "nao-responda@pklots.local";
        public string NomeRemetente { get; set; } = "PkLots";
        public string Seguranca { get; set; } = "auto"; // auto, nenhuma, starttls, ssl

        public bool Configurado => !string.IsNullOrWhiteSpace(Host);
    }
}
