using Microsoft.EntityFrameworkCore;
using PkLots.Application.Emails;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Convite de admin/operador: a conta nasce sem senha e a pessoa cria a dela pelo link do e-mail.
    // Ninguém (nem o admin que cadastrou) conhece a senha de outra pessoa.
    internal static class ConvitesUsuario
    {
        // Senha impossível de usar enquanto a conta aguarda ativação (o login nem chega a conferir)
        public static string SenhaInutilizavel(IHashSenha hashSenha) => hashSenha.Gerar(TokenSeguro.Gerar(TokenSeguro.BytesEmail));

        // Invalida convites anteriores, gera o novo e enfileira o e-mail com empresa, perfil e pátios
        public static async Task EnviarAsync(IPkLotsDbContext db, LinksPublicos links, Usuario usuario, DateTime agora, CancellationToken ct)
        {
            // A empresa pode ter acabado de ser criada (ainda não salva): procura primeiro no que está em memória
            var nomeEmpresa = db.Empresas.Local.FirstOrDefault(e => e.Id == usuario.EmpresaId)?.Nome
                ?? await db.Empresas.IgnoreQueryFilters().Where(e => e.Id == usuario.EmpresaId).Select(e => e.Nome).FirstOrDefaultAsync(ct)
                ?? "sua empresa";
            var idsPatios = usuario.Patios.Select(up => up.PatioId).ToList();
            var patios = await db.Patios.IgnoreQueryFilters()
                .Where(p => idsPatios.Contains(p.Id)).OrderBy(p => p.Nome).Select(p => p.Nome).ToListAsync(ct);

            db.TokensUsuario.RemoveRange(await db.TokensUsuario.IgnoreQueryFilters()
                .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TiposToken.ConviteUsuario).ToListAsync(ct));

            var token = TokenSeguro.Gerar(TokenSeguro.BytesEmail);
            db.TokensUsuario.Add(new TokenUsuario
            {
                Tipo = TiposToken.ConviteUsuario,
                HashToken = TokenSeguro.Hash(token),
                Email = usuario.Email,
                UsuarioId = usuario.Id,
                ExpiraEm = agora + TiposToken.ValidadeConvite,
                CriadoEm = agora
            });
            FilaEmails.Enfileirar(db, usuario.Email,
                ModelosEmail.ConviteUsuario(usuario.NomeCompleto, nomeEmpresa, usuario.Perfil, patios, links.AtivarConta(token),
                    links.UrlBase, TiposToken.ValidadeConvite), agora);
        }
    }
}
