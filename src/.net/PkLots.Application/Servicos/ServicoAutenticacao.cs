using Microsoft.EntityFrameworkCore;
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
        IContextoPatio contextoPatio,
        TimeProvider relogio)
    {
        public async Task<TokenDto> LoginAsync(LoginRequisicao requisicao, CancellationToken ct = default)
        {
            var email = requisicao.Email.Trim().ToLowerInvariant();
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (usuario is null || !usuario.Ativo || !hashSenha.Verificar(requisicao.Senha, usuario.SenhaHash))
                throw new ExcecaoNaoAutorizado("E-mail ou senha inválidos.");

            usuario.UltimoLogin = relogio.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            return geradorToken.Gerar(usuario);
        }

        // Cadastro de motorista pelo app ("Criar Conta")
        public async Task<TokenDto> RegistrarMotoristaAsync(RegistroRequisicao requisicao, CancellationToken ct = default)
        {
            var email = requisicao.Email.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                throw new ExcecaoNegocio("E-mail inválido.");
            if (string.IsNullOrWhiteSpace(requisicao.Senha) || requisicao.Senha.Length < 8)
                throw new ExcecaoNegocio("A senha deve ter pelo menos 8 caracteres.");
            if (string.IsNullOrWhiteSpace(requisicao.NomeCompleto))
                throw new ExcecaoNegocio("Nome completo é obrigatório.");
            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
                throw new ExcecaoConflito("E-mail já cadastrado.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            var usuario = new Usuario
            {
                PatioId = await contextoPatio.ObterPatioIdAsync(ct),
                Email = email,
                SenhaHash = hashSenha.Gerar(requisicao.Senha),
                NomeCompleto = requisicao.NomeCompleto.Trim(),
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
