using PkLots.Application.Interfaces;
using PkLots.Core.Entities;

namespace PkLots.Application.Emails
{
    // Grava o e-mail na fila (mesma transação da regra de negócio); o envio é feito em segundo plano
    public static class FilaEmails
    {
        public static void Enfileirar(IPkLotsDbContext db, string para, MensagemEmail mensagem, DateTime agora) =>
            db.EmailsPendentes.Add(new EmailPendente
            {
                Para = para,
                Assunto = mensagem.Assunto,
                CorpoHtml = mensagem.Html,
                CorpoTexto = mensagem.Texto,
                ProximaTentativaEm = agora,
                CriadoEm = agora
            });
    }

    public record MensagemEmail(string Assunto, string Html, string Texto);
}
