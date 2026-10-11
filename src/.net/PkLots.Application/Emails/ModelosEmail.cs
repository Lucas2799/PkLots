using System.Net;

namespace PkLots.Application.Emails
{
    // Textos dos e-mails (HTML simples + texto puro para clientes sem HTML)
    public static class ModelosEmail
    {
        public static MensagemEmail RedefinirSenha(string nome, string link, TimeSpan validade) =>
            Montar(
                "Redefinição de senha do PkLots",
                $"Olá, {nome}!",
                ["Recebemos um pedido para redefinir a senha da sua conta no PkLots."],
                "Criar nova senha",
                link,
                $"O link vale por {Horas(validade)} e só pode ser usado uma vez. Se você não pediu a redefinição, ignore este e-mail: sua senha continua a mesma.");

        private static string Horas(TimeSpan validade) =>
            validade.TotalHours == 1 ? "1 hora" : $"{validade.TotalHours:0} horas";

        private static MensagemEmail Montar(string assunto, string saudacao, string[] paragrafos, string botao, string link, string rodape)
        {
            static string H(string texto) => WebUtility.HtmlEncode(texto);

            var html = $"""
                <!doctype html>
                <html lang="pt-BR"><body style="margin:0;padding:24px;background:#f6f7f9;font-family:Arial,Helvetica,sans-serif;color:#0b2545">
                <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:28px">
                  <p style="margin:0 0 20px;font-size:22px;font-weight:bold;color:#0b2545">Pk<span style="color:#ffab00">Lots</span></p>
                  <p style="font-size:16px;font-weight:bold">{H(saudacao)}</p>
                  {string.Concat(paragrafos.Select(p => $"<p style=\"font-size:15px;line-height:1.5\">{H(p)}</p>"))}
                  <p style="margin:28px 0"><a href="{H(link)}" style="background:#0b2545;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:bold;display:inline-block">{H(botao)}</a></p>
                  <p style="font-size:13px;color:#55627a;line-height:1.5">Se o botão não funcionar, copie e cole este endereço no navegador:<br><span style="word-break:break-all">{H(link)}</span></p>
                  <p style="font-size:13px;color:#55627a;line-height:1.5">{H(rodape)}</p>
                </div>
                </body></html>
                """;

            var texto = $"{saudacao}\n\n{string.Join("\n\n", paragrafos)}\n\n{botao}: {link}\n\n{rodape}\n";
            return new MensagemEmail(assunto, html, texto);
        }
    }
}
