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
        TimeProvider relogio)
    {
        public async Task<TokenDto> LoginAsync(LoginRequisicao requisicao, CancellationToken ct = default)
        {
            var email = requisicao.Email.Trim().ToLowerInvariant();
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (usuario is null || !usuario.Ativo || !hashSenha.Verificar(requisicao.Senha, usuario.SenhaHash))
                throw new ExcecaoNaoAutorizado("E-mail ou senha inválidos.");

            if (usuario.EmpresaId is not null &&
                !await db.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Id == usuario.EmpresaId && e.Ativa, ct))
                throw new ExcecaoNaoAutorizado("O acesso desta empresa está desativado. Procure o suporte.");

            usuario.UltimoLogin = relogio.GetUtcNow().UtcDateTime;
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
