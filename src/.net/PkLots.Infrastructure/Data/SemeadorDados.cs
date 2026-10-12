using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    public class OpcoesPlataforma
    {
        // Empresa/pátio/usuários de demonstração em banco vazio (somente desenvolvimento)
        public bool CriarDadosDemonstracao { get; set; }

        // Endereço público do gateway (QR do ticket e links dos e-mails), ex.: https://pklots.com.br
        public string? UrlPublica { get; set; }
    }

    public static class SemeadorDados
    {
        // Contas que já existiram com senhas públicas (demonstração/documentação)
        private static readonly string[] EmailsDemonstracao =
            ["superadmin@pklots.local", "admin@pklots.local", "operator@pklots.local", "driver@example.com"];

        public static async Task SemearAsync(PkLotsDbContext db, IHashSenha hashSenha, OpcoesPlataforma opcoes,
            ILogger? logger = null, CancellationToken ct = default)
        {
            if (opcoes.CriarDadosDemonstracao)
                await SemearDemonstracaoAsync(db, hashSenha, logger, ct);

            await MarcarSenhasConhecidasAsync(db, hashSenha, logger, ct);

            if (!await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Perfil == Perfis.SuperAdmin && u.Ativo, ct))
                logger?.LogWarning("Nenhum superusuário cadastrado. Crie com: PkLots.Api criar-superadmin --email voce@empresa.com");
        }

        // Cria o superusuário (ou redefine a senha, se já existir) com senha temporária aleatória.
        // A senha é mostrada uma única vez e precisa ser trocada no primeiro acesso.
        public static async Task<(string Senha, bool Criado)> CriarOuRedefinirSuperAdminAsync(PkLotsDbContext db, IHashSenha hashSenha,
            string email, CancellationToken ct = default)
        {
            var emailNormalizado = RegrasUsuario.NormalizarEmail(email);
            var senha = GerarSenhaTemporaria();
            var agora = DateTime.UtcNow;

            var usuario = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == emailNormalizado, ct);
            var criado = usuario is null;
            if (usuario is null)
            {
                usuario = new Usuario { Email = emailNormalizado, NomeCompleto = "Administrador da Plataforma", Perfil = Perfis.SuperAdmin, CriadoEm = agora };
                db.Usuarios.Add(usuario);
            }
            else if (usuario.Perfil != Perfis.SuperAdmin)
            {
                throw new InvalidOperationException($"O e-mail {emailNormalizado} já pertence a um usuário com perfil {usuario.Perfil}.");
            }

            usuario.SenhaHash = hashSenha.Gerar(senha);
            usuario.DeveTrocarSenha = true;
            usuario.Ativo = true;
            usuario.FalhasLogin = 0;
            usuario.BloqueadoAte = null;
            usuario.AtualizadoEm = agora;

            await db.SaveChangesAsync(ct);
            return (senha, criado);
        }

        // 16 caracteres aleatórios (sem caracteres que se confundem), sempre com letras, números e símbolo
        public static string GerarSenhaTemporaria()
        {
            const string letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ";
            const string numeros = "23456789";
            const string simbolos = "!@#$%*-_";
            const string todos = letras + numeros + simbolos;

            var senha = new char[16];
            senha[0] = letras[RandomNumberGenerator.GetInt32(letras.Length)];
            senha[1] = numeros[RandomNumberGenerator.GetInt32(numeros.Length)];
            senha[2] = simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)];
            for (var i = 3; i < senha.Length; i++)
                senha[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];
            RandomNumberGenerator.Shuffle(senha.AsSpan());
            return new string(senha);
        }

        // Contas de demonstração que ainda usam senha pública: troca obrigatória no próximo login
        private static async Task MarcarSenhasConhecidasAsync(PkLotsDbContext db, IHashSenha hashSenha, ILogger? logger, CancellationToken ct)
        {
            var candidatos = await db.Usuarios.IgnoreQueryFilters()
                .Where(u => !u.DeveTrocarSenha && (EmailsDemonstracao.Contains(u.Email) || u.Perfil == Perfis.SuperAdmin))
                .ToListAsync(ct);

            foreach (var usuario in candidatos.Where(u => RegrasUsuario.SenhasConhecidas.Any(s => hashSenha.Verificar(s, u.SenhaHash))))
            {
                usuario.DeveTrocarSenha = true;
                logger?.LogWarning("A conta {Email} usa uma senha pública: troca obrigatória no próximo login.", usuario.Email);
            }
            await db.SaveChangesAsync(ct);
        }

        private static async Task SemearDemonstracaoAsync(PkLotsDbContext db, IHashSenha hashSenha, ILogger? logger, CancellationToken ct)
        {
            if (await db.Empresas.IgnoreQueryFilters().AnyAsync(ct))
                return;

            var agora = DateTime.UtcNow;
            var empresa = new Empresa { Nome = "Empresa Padrão" };
            var patio = ServicoPatios.NovoPatio(empresa.Id, "Pátio Padrão", 30, agora);
            db.Empresas.Add(empresa);
            db.Patios.Add(patio);
            // Plano Cortesia, criado pela migration
            db.Assinaturas.Add(new Assinatura { EmpresaId = empresa.Id, PlanoId = RecursosPlano.PlanoCortesiaId, CriadoEm = agora, AtualizadoEm = agora });

            var operador = NovoUsuario(empresa.Id, "operator@pklots.local", "Operator@123", "Operador", Perfis.Operador, hashSenha);
            operador.Patios.Add(new UsuarioPatio { PatioId = patio.Id });

            // Mensalista com acesso ao app já ativado (no uso real, o vínculo vem do convite por e-mail)
            var motorista = NovoUsuario(null, "driver@example.com", "Driver@123", "Motorista Exemplo", Perfis.Motorista, hashSenha);
            var mensalista = new Mensalista
            {
                PatioId = patio.Id,
                Nome = "Motorista Exemplo",
                Email = motorista.Email,
                UsuarioId = motorista.Id,
                VinculadoEm = agora,
                ValidoAte = DateOnly.FromDateTime(agora).AddMonths(1)
            };
            mensalista.Placas.Add(new MensalistaPlaca { MensalistaId = mensalista.Id, PatioId = patio.Id, Placa = "ABC1D23" });

            db.Usuarios.AddRange(
                NovoUsuario(empresa.Id, "admin@pklots.local", "Admin@123", "Administrador", Perfis.Admin, hashSenha),
                operador,
                motorista);
            db.Mensalistas.Add(mensalista);

            await db.SaveChangesAsync(ct);
            logger?.LogWarning("Dados de demonstração criados (senhas públicas, troca obrigatória). Não use em produção.");
        }

        private static Usuario NovoUsuario(Guid? empresaId, string email, string senha, string nome, string perfil, IHashSenha hashSenha) =>
            new()
            {
                EmpresaId = empresaId,
                Email = email,
                SenhaHash = hashSenha.Gerar(senha),
                NomeCompleto = nome,
                Perfil = perfil
            };
    }
}
