namespace PkLots.Application.DTOs
{
    public record LoginRequisicao(string Email, string Senha);

    public record RegistroRequisicao(string Email, string Senha, string NomeCompleto);

    public record TokenDto(string TokenAcesso, int ExpiraEm, string Perfil);
}
