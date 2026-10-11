using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Email
{
    public class EnviadorEmailSmtp(IOptions<OpcoesEmail> opcoes)
    {
        public async Task EnviarAsync(IReadOnlyList<EmailPendente> emails, Func<EmailPendente, Exception?, Task> aoTerminar,
            CancellationToken ct)
        {
            var config = opcoes.Value;
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(config.Host, config.Porta, Seguranca(config.Seguranca), ct);
            if (!string.IsNullOrWhiteSpace(config.Usuario))
                await smtp.AuthenticateAsync(config.Usuario, config.Senha ?? "", ct);

            foreach (var email in emails)
            {
                try
                {
                    await smtp.SendAsync(Montar(config, email), ct);
                    await aoTerminar(email, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await aoTerminar(email, ex);
                }
            }
            await smtp.DisconnectAsync(true, ct);
        }

        private static MimeMessage Montar(OpcoesEmail config, EmailPendente email)
        {
            var mensagem = new MimeMessage();
            mensagem.From.Add(new MailboxAddress(config.NomeRemetente, config.Remetente));
            mensagem.To.Add(MailboxAddress.Parse(email.Para));
            mensagem.Subject = email.Assunto;
            mensagem.Body = new BodyBuilder { HtmlBody = email.CorpoHtml, TextBody = email.CorpoTexto }.ToMessageBody();
            return mensagem;
        }

        private static SecureSocketOptions Seguranca(string valor) => valor.Trim().ToLowerInvariant() switch
        {
            "nenhuma" => SecureSocketOptions.None,
            "starttls" => SecureSocketOptions.StartTls,
            "ssl" => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.Auto
        };
    }
}
