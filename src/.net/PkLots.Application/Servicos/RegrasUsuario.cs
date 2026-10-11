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

        // Mínimo de 8 caracteres, com letras e números; senhas de demonstração do sistema não são aceitas
        public static void ValidarSenha(string? senha)
        {
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8)
                throw new ExcecaoNegocio("A senha deve ter pelo menos 8 caracteres.");
            if (senha.Length > 128)
                throw new ExcecaoNegocio("A senha deve ter no máximo 128 caracteres.");
            if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit))
                throw new ExcecaoNegocio("A senha deve ter letras e números.");
            if (SenhasConhecidas.Contains(senha))
                throw new ExcecaoNegocio("Essa senha é conhecida (de demonstração). Escolha outra.");
        }

        // Senhas que já circularam (dados de demonstração e documentação): nunca valem como senha real
        public static readonly HashSet<string> SenhasConhecidas =
            ["Admin@123", "Operator@123", "Driver@123", "SuperAdmin@123"];

        public static string ValidarNome(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ExcecaoNegocio("Nome completo é obrigatório.");
            return nome.Trim();
        }

        public static readonly string[] PerfisDeEmpresa = [Perfis.Admin, Perfis.Operador];

        // Usuários criados pelo admin de uma empresa (mensalistas entram por convite; superadmin é da plataforma)
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
