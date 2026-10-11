namespace PkLots.Application.DTOs
{
    public record LoginRequisicao(string Email, string Senha);

    public record TokenDto(string TokenAcesso, int ExpiraEm, string Perfil, bool DeveTrocarSenha = false);

    public record TrocarSenhaRequisicao(string SenhaAtual, string NovaSenha);
}
