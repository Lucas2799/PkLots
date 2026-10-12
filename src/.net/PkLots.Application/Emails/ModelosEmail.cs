using System.Net;

namespace PkLots.Application.Emails
{
    // Textos dos e-mails (HTML simples + texto puro para clientes sem HTML)
    public static class ModelosEmail
    {
        public static MensagemEmail ConviteMensalista(string nome, string nomePatio, string link, TimeSpan validade) =>
            Montar(
                $"Seu acesso ao {nomePatio} no PkLots",
                $"Olá, {nome}!",
                [
                    $"O {nomePatio} cadastrou você como mensalista e liberou o acesso ao aplicativo PkLots.",
                    "Por ele você acompanha a validade da mensalidade e os veículos do seu contrato no pátio."
                ],
                "Ativar meu acesso",
                link,
                $"O link vale por {Horas(validade)} e só pode ser usado uma vez. Se você não esperava este e-mail, ignore-o: nenhum acesso é liberado sem abrir o link.");

        public static MensagemEmail ConviteUsuario(string nome, string nomeEmpresa, string perfil, IReadOnlyList<string> patios,
            string link, string enderecoSistema, TimeSpan validade)
        {
            var admin = perfil == PkLots.Core.Constantes.Perfis.Admin;
            var paragrafos = new List<string>
            {
                $"Você foi cadastrado(a) no PkLots, o sistema de gestão do estacionamento da {nomeEmpresa}, como {(admin ? "Administrador(a)" : "Operador(a)")}.",
                admin
                    ? "Como administrador(a) você acessa todos os pátios da empresa: painel, entrada e saída de veículos, caixa, mensalistas e a área de Administração (preços, vagas, usuários, pagamentos e impressão)."
                    : $"Pátio(s) em que você vai trabalhar: {string.Join(", ", patios)}. Você acessa o painel, a entrada e a saída de veículos, o seu caixa e os mensalistas.",
                $"Para começar, crie a sua senha pelo botão abaixo. Depois, entre sempre pelo endereço {enderecoSistema} com o seu e-mail e essa senha."
            };
            return Montar($"Seu acesso ao PkLots ({nomeEmpresa})", $"Olá, {nome}!", [.. paragrafos], "Criar minha senha", link,
                $"O link vale por {Horas(validade)} e só pode ser usado uma vez. Se expirar, peça ao administrador para reenviar o convite. Ninguém do PkLots ou da empresa vai pedir a sua senha.");
        }

        public static MensagemEmail RedefinirSenha(string nome, string link, TimeSpan validade) =>
            Montar(
                "Redefinição de senha do PkLots",
                $"Olá, {nome}!",
                ["Recebemos um pedido para redefinir a senha da sua conta no PkLots."],
                "Criar nova senha",
                link,
                $"O link vale por {Horas(validade)} e só pode ser usado uma vez. Se você não pediu a redefinição, ignore este e-mail: sua senha continua a mesma.");

        // Fatura do aluguel do PkLots para os administradores da empresa
        public static MensagemEmail FaturaEmitida(string nomeEmpresa, string descricao, string valor, DateOnly vencimento,
            string? pixCopiaECola, string enderecoSistema)
        {
            var paragrafos = new List<string>
            {
                $"A fatura do PkLots da {nomeEmpresa} está disponível.",
                $"{descricao}: {valor}, com vencimento em {vencimento:dd/MM/yyyy}."
            };
            paragrafos.Add(pixCopiaECola is null
                ? "Os dados para pagamento estão no sistema, em Administração > Assinatura."
                : $"Para pagar por PIX, use o código copia e cola abaixo (ou o QR Code em Administração > Assinatura): {pixCopiaECola}");
            return Montar($"Fatura do PkLots: {descricao} ({valor})", "Olá!", [.. paragrafos], "Abrir o PkLots", enderecoSistema,
                "Depois do pagamento, a baixa é feita pela equipe do PkLots. Se já pagou, desconsidere este e-mail.");
        }

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
