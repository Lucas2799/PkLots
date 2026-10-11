using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Emails;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Acesso do mensalista ao app. O operador informa um e-mail no cadastro; o sistema envia um convite e só
    // quem abre o link desse e-mail fica vinculado ao contrato. O pátio atesta a pessoa; o e-mail, a conta.
    public class ServicoAcessoMensalista(
        IPkLotsDbContext db,
        IHashSenha hashSenha,
        IGeradorToken geradorToken,
        TimeProvider relogio)
    {
        private const string ConviteInvalido = "Convite inválido, expirado ou já utilizado. Peça ao estacionamento para reenviar.";

        // ---------- Lado do pátio (chamado pelo cadastro de mensalistas) ----------

        // Aplica o e-mail informado no cadastro: novo e-mail → novo convite (e o vínculo anterior cai);
        // e-mail removido → acesso revogado; mesmo e-mail → nada muda
        internal static void AplicarEmail(IPkLotsDbContext db, LinksPublicos links, Mensalista mensalista, string? emailInformado,
            string nomePatio, Guid usuarioId, DateTime agora)
        {
            var email = string.IsNullOrWhiteSpace(emailInformado) ? null : RegrasUsuario.NormalizarEmail(emailInformado);
            if (email == mensalista.Email)
                return;

            var tinhaAcesso = mensalista.UsuarioId is not null || mensalista.Email is not null;
            RevogarAcesso(db, mensalista);
            if (tinhaAcesso)
                Auditoria.Registrar(db, mensalista.PatioId, AcoesAuditoria.AcessoMensalistaRevogado, SituacaoAuditoria.Sucesso, null, agora,
                    new() { ["usuarioId"] = usuarioId.ToString(), ["mensalista"] = mensalista.Nome, ["email"] = mensalista.Email ?? "" });

            mensalista.Email = email;
            if (email is not null)
                EnviarConvite(db, links, mensalista, nomePatio, usuarioId, agora);
        }

        internal static void EnviarConvite(IPkLotsDbContext db, LinksPublicos links, Mensalista mensalista, string nomePatio,
            Guid usuarioId, DateTime agora)
        {
            db.TokensUsuario.RemoveRange(db.TokensUsuario.IgnoreQueryFilters().Where(t => t.MensalistaId == mensalista.Id && t.Tipo == TiposToken.ConviteMensalista));

            var token = TokenSeguro.Gerar(TokenSeguro.BytesEmail);
            db.TokensUsuario.Add(new TokenUsuario
            {
                Tipo = TiposToken.ConviteMensalista,
                HashToken = TokenSeguro.Hash(token),
                Email = mensalista.Email!,
                MensalistaId = mensalista.Id,
                ExpiraEm = agora + TiposToken.ValidadeConvite,
                CriadoEm = agora
            });
            FilaEmails.Enfileirar(db, mensalista.Email!,
                ModelosEmail.ConviteMensalista(mensalista.Nome, nomePatio, links.Convite(token), TiposToken.ValidadeConvite), agora);

            Auditoria.Registrar(db, mensalista.PatioId, AcoesAuditoria.ConviteMensalistaEnviado, SituacaoAuditoria.Sucesso,
                mensalista.Placas.FirstOrDefault()?.Placa, agora,
                new() { ["usuarioId"] = usuarioId.ToString(), ["mensalista"] = mensalista.Nome, ["email"] = mensalista.Email! });
        }

        private static void RevogarAcesso(IPkLotsDbContext db, Mensalista mensalista)
        {
            mensalista.UsuarioId = null;
            mensalista.VinculadoEm = null;
            db.TokensUsuario.RemoveRange(db.TokensUsuario.IgnoreQueryFilters().Where(t => t.MensalistaId == mensalista.Id && t.Tipo == TiposToken.ConviteMensalista));
        }

        public static string SituacaoAcesso(Mensalista m) =>
            m.Email is null ? AcessoAppMensalista.SemEmail
            : m.UsuarioId is null ? AcessoAppMensalista.ConvitePendente
            : AcessoAppMensalista.Ativo;

        // ---------- Lado do mensalista (link do e-mail, sem login) ----------

        public async Task<ConviteMensalistaDto> ObterConviteAsync(string token, CancellationToken ct = default)
        {
            var (convite, mensalista) = await ValidarConviteAsync(token, ct);
            var contaExistente = await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == convite.Email, ct);
            return new ConviteMensalistaDto(mensalista.Nome, mensalista.Patio.Nome, convite.Email,
                mensalista.Placas.Select(p => MascararPlaca(p.Placa)).OrderBy(p => p).ToList(), contaExistente);
        }

        public async Task<AceiteConviteDto> AceitarConviteAsync(string token, AceitarConviteRequisicao requisicao, CancellationToken ct = default)
        {
            var (convite, mensalista) = await ValidarConviteAsync(token, ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            var usuario = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == convite.Email, ct);
            var contaCriada = usuario is null;
            if (usuario is null)
            {
                RegrasUsuario.ValidarSenha(requisicao.Senha);
                usuario = new Usuario
                {
                    Email = convite.Email,
                    SenhaHash = hashSenha.Gerar(requisicao.Senha!),
                    NomeCompleto = string.IsNullOrWhiteSpace(requisicao.NomeCompleto) ? mensalista.Nome : RegrasUsuario.ValidarNome(requisicao.NomeCompleto),
                    Perfil = Perfis.Motorista,
                    UltimoLogin = agora,
                    CriadoEm = agora,
                    AtualizadoEm = agora
                };
                db.Usuarios.Add(usuario);
            }
            else if (usuario.Perfil != Perfis.Motorista)
            {
                // Conta de gestão (admin/operador/superadmin) não mistura acesso de cliente
                throw new ExcecaoConflito("Este e-mail já é usado por uma conta de gestão do PkLots. Peça ao estacionamento para cadastrar outro e-mail.");
            }
            else if (!usuario.Ativo)
            {
                throw new ExcecaoConflito("A conta deste e-mail está desativada. Procure o suporte do PkLots.");
            }

            mensalista.UsuarioId = usuario.Id;
            mensalista.VinculadoEm = agora;
            convite.UsadoEm = agora;

            Auditoria.Registrar(db, mensalista.PatioId, AcoesAuditoria.ConviteMensalistaAceito, SituacaoAuditoria.Sucesso,
                mensalista.Placas.FirstOrDefault()?.Placa, agora,
                new() { ["mensalista"] = mensalista.Nome, ["email"] = convite.Email, ["contaCriada"] = contaCriada });

            await db.SaveChangesAsync(ct);
            return new AceiteConviteDto(contaCriada, contaCriada ? geradorToken.Gerar(usuario) : null);
        }

        private async Task<(TokenUsuario Convite, Mensalista Mensalista)> ValidarConviteAsync(string token, CancellationToken ct)
        {
            if (!TokenSeguro.FormatoValido(token, TokenSeguro.BytesEmail))
                throw new ExcecaoNaoEncontrado(ConviteInvalido);

            var hash = TokenSeguro.Hash(token);
            var convite = await db.TokensUsuario.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.HashToken == hash && t.Tipo == TiposToken.ConviteMensalista, ct);
            var agora = relogio.GetUtcNow().UtcDateTime;
            if (convite is null || convite.UsadoEm is not null || convite.ExpiraEm <= agora || convite.MensalistaId is null)
                throw new ExcecaoNaoEncontrado(ConviteInvalido);

            // O contrato precisa continuar ativo e com o mesmo e-mail do convite
            var mensalista = await db.Mensalistas
                .Include(m => m.Placas)
                .Include(m => m.Patio)
                .FirstOrDefaultAsync(m => m.Id == convite.MensalistaId && m.Ativo && m.Email == convite.Email
                    && m.Patio.Ativo && m.Patio.Empresa.Ativa, ct)
                ?? throw new ExcecaoNaoEncontrado(ConviteInvalido);

            return (convite, mensalista);
        }

        // "ABC1D23" → "ABC-**23": a pessoa reconhece o carro, quem recebeu o e-mail por engano não descobre a placa
        public static string MascararPlaca(string placa) =>
            placa.Length == 7 ? $"{placa[..3]}-**{placa[5..]}" : "***";
    }
}
