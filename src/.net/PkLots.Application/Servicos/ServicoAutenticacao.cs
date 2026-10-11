using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PkLots.Application.DTOs;
using PkLots.Application.Emails;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    public class ServicoAutenticacao(
        IPkLotsDbContext db,
        IHashSenha hashSenha,
        IGeradorToken geradorToken,
        LinksPublicos links,
        TimeProvider relogio,
        ILogger<ServicoAutenticacao>? logger = null)
    {
        public const int MaximoFalhas = 5;
        public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);

        public async Task<TokenDto> LoginAsync(LoginRequisicao requisicao, CancellationToken ct = default)
        {
            var email = (requisicao.Email ?? "").Trim().ToLowerInvariant();
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            // Mesma mensagem para e-mail inexistente e senha errada (não revela quem tem conta).
            // Conta convidada que ainda não criou a senha também não entra.
            const string invalido = "E-mail ou senha inválidos.";
            if (usuario is null || !usuario.Ativo || usuario.AguardandoAtivacao)
            {
                logger?.LogWarning("Login recusado para {Email}: usuário inexistente ou inativo", email);
                throw new ExcecaoNaoAutorizado(invalido);
            }

            if (usuario.BloqueadoAte is { } ate && ate > agora)
            {
                var minutos = (int)Math.Ceiling((ate - agora).TotalMinutes);
                throw new ExcecaoNaoAutorizado($"Muitas tentativas incorretas. Tente novamente em {minutos} minuto(s).");
            }

            if (!hashSenha.Verificar(requisicao.Senha ?? "", usuario.SenhaHash))
            {
                usuario.FalhasLogin++;
                if (usuario.FalhasLogin >= MaximoFalhas)
                {
                    usuario.BloqueadoAte = agora + TempoBloqueio;
                    usuario.FalhasLogin = 0;
                    logger?.LogWarning("Conta {Email} bloqueada por {Minutos} min após {Falhas} tentativas", email, TempoBloqueio.TotalMinutes, MaximoFalhas);
                }
                await db.SaveChangesAsync(ct);
                throw new ExcecaoNaoAutorizado(invalido);
            }

            if (usuario.EmpresaId is not null &&
                !await db.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Id == usuario.EmpresaId && e.Ativa, ct))
                throw new ExcecaoNaoAutorizado("O acesso desta empresa está desativado. Procure o suporte.");

            usuario.FalhasLogin = 0;
            usuario.BloqueadoAte = null;
            usuario.UltimoLogin = agora;
            await db.SaveChangesAsync(ct);

            return geradorToken.Gerar(usuario);
        }

        // Troca da própria senha (obrigatória quando a senha é temporária ou de demonstração)
        public async Task<TokenDto> TrocarSenhaAsync(Guid usuarioId, TrocarSenhaRequisicao requisicao, CancellationToken ct = default)
        {
            var usuario = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct)
                ?? throw new ExcecaoNaoAutorizado("Usuário não encontrado.");

            if (!hashSenha.Verificar(requisicao.SenhaAtual ?? "", usuario.SenhaHash))
                throw new ExcecaoNegocio("A senha atual não confere.");
            RegrasUsuario.ValidarSenha(requisicao.NovaSenha);
            if (requisicao.NovaSenha == requisicao.SenhaAtual)
                throw new ExcecaoNegocio("A nova senha deve ser diferente da atual.");

            usuario.SenhaHash = hashSenha.Gerar(requisicao.NovaSenha);
            usuario.DeveTrocarSenha = false;
            usuario.AtualizadoEm = relogio.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            return geradorToken.Gerar(usuario);
        }

        // "Esqueci minha senha": responde igual exista ou não a conta (não revela quem tem cadastro).
        // Superadmin recupera o acesso só pelo servidor (comando criar-superadmin).
        public async Task SolicitarRedefinicaoAsync(EsqueciSenhaRequisicao requisicao, CancellationToken ct = default)
        {
            var email = (requisicao.Email ?? "").Trim().ToLowerInvariant();
            if (email.Length == 0)
                throw new ExcecaoNegocio("Informe o e-mail.");

            var usuario = await db.Usuarios.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email && u.Ativo && u.Perfil != Perfis.SuperAdmin
                    && (u.Empresa == null || u.Empresa.Ativa), ct);
            if (usuario is null)
            {
                logger?.LogInformation("Redefinição de senha pedida para e-mail sem conta ativa: {Email}", email);
                return;
            }

            var agora = relogio.GetUtcNow().UtcDateTime;
            // Ainda não ativou: manda de novo o convite (que é por onde se cria a primeira senha)
            if (usuario.AguardandoAtivacao)
            {
                await db.Usuarios.Entry(usuario).Collection(u => u.Patios).LoadAsync(ct);
                await ConvitesUsuario.EnviarAsync(db, links, usuario, agora, ct);
            }
            else
            {
                EnfileirarRedefinicao(db, links, usuario, agora);
            }
            await db.SaveChangesAsync(ct);
        }

        // Link de nova senha (também usado quando o admin pede a redefinição para alguém da equipe)
        internal static void EnfileirarRedefinicao(IPkLotsDbContext db, LinksPublicos links, Usuario usuario, DateTime agora)
        {
            db.TokensUsuario.RemoveRange(db.TokensUsuario.IgnoreQueryFilters().Where(t => t.UsuarioId == usuario.Id && t.Tipo == TiposToken.RedefinirSenha));
            var token = TokenSeguro.Gerar(TokenSeguro.BytesEmail);
            db.TokensUsuario.Add(new TokenUsuario
            {
                Tipo = TiposToken.RedefinirSenha,
                HashToken = TokenSeguro.Hash(token),
                Email = usuario.Email,
                UsuarioId = usuario.Id,
                ExpiraEm = agora + TiposToken.ValidadeRedefinicao,
                CriadoEm = agora
            });
            FilaEmails.Enfileirar(db, usuario.Email,
                ModelosEmail.RedefinirSenha(usuario.NomeCompleto, links.RedefinirSenha(token), TiposToken.ValidadeRedefinicao), agora);
        }

        // ---------- Convite de admin/operador ----------

        private const string ConviteInvalido = "Convite inválido, expirado ou já utilizado. Peça ao administrador para reenviar.";

        public async Task<ConviteUsuarioDto> ObterConviteUsuarioAsync(string token, CancellationToken ct = default)
        {
            var (_, usuario) = await ValidarConviteUsuarioAsync(token, ct);
            var empresa = await db.Empresas.IgnoreQueryFilters().Where(e => e.Id == usuario.EmpresaId).Select(e => e.Nome).FirstOrDefaultAsync(ct);
            var idsPatios = usuario.Patios.Select(up => up.PatioId).ToList();
            var patios = await db.Patios.IgnoreQueryFilters().Where(p => idsPatios.Contains(p.Id)).OrderBy(p => p.Nome).Select(p => p.Nome).ToListAsync(ct);
            return new ConviteUsuarioDto(usuario.NomeCompleto, usuario.Email, empresa ?? "", usuario.Perfil, patios);
        }

        // Cria a primeira senha e já entra no sistema
        public async Task<TokenDto> AtivarContaAsync(AtivarContaRequisicao requisicao, CancellationToken ct = default)
        {
            var (convite, usuario) = await ValidarConviteUsuarioAsync(requisicao.Token, ct);
            RegrasUsuario.ValidarSenha(requisicao.Senha);

            var agora = relogio.GetUtcNow().UtcDateTime;
            usuario.SenhaHash = hashSenha.Gerar(requisicao.Senha!);
            usuario.AguardandoAtivacao = false;
            usuario.DeveTrocarSenha = false;
            usuario.FalhasLogin = 0;
            usuario.BloqueadoAte = null;
            usuario.UltimoLogin = agora;
            usuario.AtualizadoEm = agora;
            convite.UsadoEm = agora;

            await db.SaveChangesAsync(ct);
            logger?.LogInformation("Conta ativada por convite: {Email}", usuario.Email);
            return geradorToken.Gerar(usuario);
        }

        private async Task<(TokenUsuario Convite, Usuario Usuario)> ValidarConviteUsuarioAsync(string? token, CancellationToken ct)
        {
            if (!TokenSeguro.FormatoValido(token, TokenSeguro.BytesEmail))
                throw new ExcecaoNaoEncontrado(ConviteInvalido);

            var hash = TokenSeguro.Hash(token!);
            var agora = relogio.GetUtcNow().UtcDateTime;
            var convite = await db.TokensUsuario.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.HashToken == hash && t.Tipo == TiposToken.ConviteUsuario, ct);
            if (convite is null || convite.UsadoEm is not null || convite.ExpiraEm <= agora)
                throw new ExcecaoNaoEncontrado(ConviteInvalido);

            // Conta ainda pendente, ativa, com o mesmo e-mail do convite e empresa ativa
            var usuario = await db.Usuarios.IgnoreQueryFilters()
                .Include(u => u.Patios)
                .FirstOrDefaultAsync(u => u.Id == convite.UsuarioId && u.Ativo && u.AguardandoAtivacao && u.Email == convite.Email
                    && (u.Empresa == null || u.Empresa.Ativa), ct)
                ?? throw new ExcecaoNaoEncontrado(ConviteInvalido);
            return (convite, usuario);
        }

        // Nova senha pelo link do e-mail: também desbloqueia a conta e encerra a senha temporária
        public async Task RedefinirSenhaAsync(RedefinirSenhaPorEmailRequisicao requisicao, CancellationToken ct = default)
        {
            const string invalido = "Link inválido, expirado ou já utilizado. Peça uma nova redefinição de senha.";
            if (!TokenSeguro.FormatoValido(requisicao.Token, TokenSeguro.BytesEmail))
                throw new ExcecaoNaoEncontrado(invalido);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var hash = TokenSeguro.Hash(requisicao.Token!);
            var pedido = await db.TokensUsuario.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.HashToken == hash && t.Tipo == TiposToken.RedefinirSenha, ct);
            if (pedido is null || pedido.UsadoEm is not null || pedido.ExpiraEm <= agora)
                throw new ExcecaoNaoEncontrado(invalido);

            var usuario = await db.Usuarios.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == pedido.UsuarioId && u.Ativo && u.Email == pedido.Email, ct)
                ?? throw new ExcecaoNaoEncontrado(invalido);

            RegrasUsuario.ValidarSenha(requisicao.NovaSenha);
            usuario.SenhaHash = hashSenha.Gerar(requisicao.NovaSenha!);
            usuario.DeveTrocarSenha = false;
            usuario.FalhasLogin = 0;
            usuario.BloqueadoAte = null;
            usuario.AtualizadoEm = agora;
            pedido.UsadoEm = agora;

            await db.SaveChangesAsync(ct);
            logger?.LogInformation("Senha redefinida por e-mail para {Email}", usuario.Email);
        }
    }
}
