using PkLots.Application.Excecoes;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Validações de cadastro de usuário compartilhadas entre o cadastro público e a administração
    public static class RegrasUsuario
    {
        public static readonly string[] PerfisValidos = [Perfis.Admin, Perfis.Operador, Perfis.Motorista];

        public static string NormalizarEmail(string? email)
        {
            var normalizado = (email ?? "").Trim().ToLowerInvariant();
            if (normalizado.Length == 0 || !normalizado.Contains('@') || normalizado.Length > 255)
                throw new ExcecaoNegocio("E-mail inválido.");
            return normalizado;
        }

        public static void ValidarSenha(string? senha)
        {
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8)
                throw new ExcecaoNegocio("A senha deve ter pelo menos 8 caracteres.");
        }

        public static string ValidarNome(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ExcecaoNegocio("Nome completo é obrigatório.");
            return nome.Trim();
        }

        public static readonly string[] PerfisDeEmpresa = [Perfis.Admin, Perfis.Operador];

        // Usuários criados pelo admin de uma empresa (motoristas se cadastram pelo app; superadmin é da plataforma)
        public static string ValidarPerfilDeEmpresa(string? perfil)
        {
            var normalizado = (perfil ?? "").Trim().ToLowerInvariant();
            if (!PerfisDeEmpresa.Contains(normalizado))
                throw new ExcecaoNegocio($"Perfil inválido. Use: {string.Join(", ", PerfisDeEmpresa)}.");
            return normalizado;
        }

        public static string ValidarPerfil(string? perfil)
        {
            var normalizado = (perfil ?? "").Trim().ToLowerInvariant();
            if (!PerfisValidos.Contains(normalizado))
                throw new ExcecaoNegocio($"Perfil inválido. Use: {string.Join(", ", PerfisValidos)}.");
            return normalizado;
        }
    }
}
