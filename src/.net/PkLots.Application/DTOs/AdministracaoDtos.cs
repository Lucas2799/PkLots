namespace PkLots.Application.DTOs
{
    public record ConfiguracaoPatioDto(string Nome, decimal ValorHora, int MinutosTolerancia, int TotalVagas, int VagasPorFileira, int MaximoVagas);

    public record AtualizarConfiguracaoRequisicao(string Nome, decimal ValorHora, int MinutosTolerancia);

    public record AlterarQuantidadeVagasRequisicao(int Total);

    public record UsuarioDto(Guid Id, string Email, string NomeCompleto, string Perfil, bool Ativo, DateTime? UltimoLogin, DateTime CriadoEm);

    public record CriarUsuarioRequisicao(string Email, string NomeCompleto, string Senha, string Perfil);

    public record AtualizarUsuarioRequisicao(string NomeCompleto, string Perfil, bool Ativo);

    public record RedefinirSenhaRequisicao(string Senha);
}
