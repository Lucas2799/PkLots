namespace PkLots.Application.DTOs
{
    public record ConfiguracaoPatioDto(string Nome, decimal ValorHora, int MinutosTolerancia, int TotalVagas, int VagasPorFileira, int MaximoVagas,
        decimal ValorMensalidade = 0, int VagasReservadasMensalistas = 0);

    // Mensalidade e vagas reservadas: nulos mantêm o valor atual
    public record AtualizarConfiguracaoRequisicao(string Nome, decimal ValorHora, int MinutosTolerancia,
        decimal? ValorMensalidade = null, int? VagasReservadasMensalistas = null);

    public record AlterarQuantidadeVagasRequisicao(int Total);

    public record UsuarioDto(Guid Id, string Email, string NomeCompleto, string Perfil, bool Ativo, DateTime? UltimoLogin, DateTime CriadoEm,
        IReadOnlyList<Guid> PatiosIds, bool ConvitePendente = false);

    // Sem senha: a pessoa recebe um convite por e-mail e cria a própria.
    // PatiosIds: obrigatório para operador (pátios em que trabalha); ignorado para admin (acessa todos)
    public record CriarUsuarioRequisicao(string Email, string NomeCompleto, string Perfil, IReadOnlyList<Guid>? PatiosIds = null);

    public record AtualizarUsuarioRequisicao(string NomeCompleto, string Perfil, bool Ativo, IReadOnlyList<Guid>? PatiosIds = null);

    // ---------- Pátios ----------

    public record PatioResumoDto(Guid Id, string Nome);

    public record PatioAdminDto(Guid Id, string Nome, bool Ativo, int TotalVagas, decimal ValorHora, int VeiculosEstacionados);

    public record CriarPatioRequisicao(string Nome, int TotalVagas = 30, decimal ValorHora = 15m, int MinutosTolerancia = 0);

    public record AtualizarPatioRequisicao(string Nome, bool Ativo);

    // ---------- Plataforma (superadmin) ----------

    public record EmpresaDto(Guid Id, string Nome, string? Documento, bool Ativa, int Patios, int Usuarios, DateTime CriadoEm,
        int ConvitesPendentes = 0);

    public record CriarEmpresaRequisicao(
        string Nome,
        string? Documento,
        string NomePatio,
        int TotalVagas,
        string NomeAdmin,
        string EmailAdmin); // o admin recebe um convite por e-mail para criar a senha

    public record AtualizarEmpresaRequisicao(string Nome, string? Documento, bool Ativa);
}
