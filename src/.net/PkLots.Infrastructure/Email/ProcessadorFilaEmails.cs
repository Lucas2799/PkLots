using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PkLots.Core.Entities;
using PkLots.Infrastructure.Data;

namespace PkLots.Infrastructure.Email
{
    // Envia a fila de e-mails em segundo plano. Falha do SMTP nunca desfaz a regra de negócio:
    // o e-mail volta para a fila com espera crescente e desiste após algumas tentativas.
    public class ProcessadorFilaEmails(
        IServiceScopeFactory escopos,
        IOptions<OpcoesEmail> opcoes,
        TimeProvider relogio,
        ILogger<ProcessadorFilaEmails> logger) : BackgroundService
    {
        public const int MaximoTentativas = 6;
        private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(10);
        private const int Lote = 20;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (!opcoes.Value.Configurado)
            {
                logger.LogWarning("SMTP não configurado (Email__Host): convites e redefinições de senha ficam na fila sem envio.");
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ProcessarAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Falha ao processar a fila de e-mails");
                }
                await Task.Delay(Intervalo, relogio, ct);
            }
        }

        private async Task ProcessarAsync(CancellationToken ct)
        {
            using var escopo = escopos.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<PkLotsDbContext>();
            var agora = relogio.GetUtcNow().UtcDateTime;

            var pendentes = await db.EmailsPendentes
                .Where(e => e.EnviadoEm == null && e.Tentativas < MaximoTentativas && e.ProximaTentativaEm <= agora)
                .OrderBy(e => e.CriadoEm)
                .Take(Lote)
                .ToListAsync(ct);
            if (pendentes.Count == 0)
                return;

            var enviador = escopo.ServiceProvider.GetRequiredService<EnviadorEmailSmtp>();
            try
            {
                await enviador.EnviarAsync(pendentes, (email, erro) =>
                {
                    var momento = relogio.GetUtcNow().UtcDateTime;
                    if (erro is null)
                    {
                        email.EnviadoEm = momento;
                        email.UltimoErro = null;
                        ApagarConteudo(email);
                    }
                    else
                    {
                        RegistrarFalha(email, erro, momento);
                    }
                    return Task.CompletedTask;
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Não conectou/autenticou no SMTP: todo o lote volta para a fila
                var momento = relogio.GetUtcNow().UtcDateTime;
                foreach (var email in pendentes.Where(e => e.EnviadoEm is null))
                    RegistrarFalha(email, ex, momento);
            }

            await db.SaveChangesAsync(CancellationToken.None);
        }

        private void RegistrarFalha(EmailPendente email, Exception erro, DateTime agora)
        {
            email.Tentativas++;
            email.UltimoErro = erro.Message.Length > 500 ? erro.Message[..500] : erro.Message;
            email.ProximaTentativaEm = agora + TimeSpan.FromMinutes(Math.Pow(2, email.Tentativas)); // 2, 4, 8... min
            if (email.Tentativas >= MaximoTentativas)
            {
                ApagarConteudo(email);
                logger.LogError(erro, "E-mail para {Para} descartado após {Tentativas} tentativas", email.Para, email.Tentativas);
            }
            else
            {
                logger.LogWarning("Falha ao enviar e-mail para {Para} (tentativa {Tentativa}): {Erro}", email.Para, email.Tentativas, erro.Message);
            }
        }

        // O corpo tem links de acesso: não fica guardado depois de enviado (ou descartado)
        private static void ApagarConteudo(EmailPendente email)
        {
            email.CorpoHtml = null;
            email.CorpoTexto = null;
        }
    }
}
