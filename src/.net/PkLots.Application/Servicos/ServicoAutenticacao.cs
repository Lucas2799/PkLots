using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PkLots.Application.DTOs;
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

            // Mesma mensagem para e-mail inexistente e senha errada (não revela quem tem conta)
            const string invalido = "E-mail ou senha inválidos.";
            if (usuario is null || !usuario.Ativo)
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

        // Cadastro de motorista pelo app ("Criar Conta")
        public async Task<TokenDto> RegistrarMotoristaAsync(RegistroRequisicao requisicao, CancellationToken ct = default)
        {
            var email = RegrasUsuario.NormalizarEmail(requisicao.Email);
            RegrasUsuario.ValidarSenha(requisicao.Senha);
            var nome = RegrasUsuario.ValidarNome(requisicao.NomeCompleto);

            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
                throw new ExcecaoConflito("E-mail já cadastrado.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            var usuario = new Usuario
            {
                Email = email,
                SenhaHash = hashSenha.Gerar(requisicao.Senha),
                NomeCompleto = nome,
                Perfil = Perfis.Motorista,
                UltimoLogin = agora,
                CriadoEm = agora,
                AtualizadoEm = agora
            };

            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync(ct);

            return geradorToken.Gerar(usuario);
        }
    }
}
