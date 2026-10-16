namespace PkLots.Application.DTOs
{
    // Filtros da tela de auditoria (datas AAAA-MM-DD no fuso de Brasília, fim inclusivo).
    // EmpresaId só vale para o superadmin; o admin vê sempre a própria empresa.
    public record FiltroAuditoria(
        DateOnly? De = null,
        DateOnly? Ate = null,
        Guid? EmpresaId = null,
        Guid? PatioId = null,
        string? Categoria = null,
        string? Situacao = null,
        string? Placa = null,
        Guid? UsuarioId = null,
        int Pagina = 1,
        int TamanhoPagina = 50);

    public record DetalheAuditoriaDto(string Rotulo, string Valor);

    public record RegistroAuditoriaDto(
        Guid Id,
        DateTime CriadoEm,
        string Acao,
        string Descricao,
        string Categoria,
        string Situacao,
        string? Placa,
        Guid PatioId,
        string Patio,
        string? Empresa,
        Guid? UsuarioId,
        string Usuario,
        IReadOnlyList<DetalheAuditoriaDto> Detalhes);

    public record PaginaAuditoriaDto(IReadOnlyList<RegistroAuditoriaDto> Itens, int Total, int Pagina, int TamanhoPagina, DateOnly De, DateOnly Ate);

    public record OpcaoFiltroDto(string Id, string Nome);

    // Listas dos filtros: categorias de ação, pátios e usuários da empresa; empresas (só superadmin)
    public record OpcoesAuditoriaDto(
        IReadOnlyList<OpcaoFiltroDto> Categorias,
        IReadOnlyList<OpcaoFiltroDto> Patios,
        IReadOnlyList<OpcaoFiltroDto> Usuarios,
        IReadOnlyList<OpcaoFiltroDto> Empresas);

    // Log técnico da API (superadmin)
    public record FiltroLogsSistema(DateOnly? De = null, DateOnly? Ate = null, string? Nivel = null, string? Texto = null,
        int Pagina = 1, int TamanhoPagina = 100);

    public record RegistroLogSistemaDto(DateTime Quando, string Nivel, string Mensagem, string? Excecao, string? Origem,
        string? Caminho, int? Status);

    public record PaginaLogsSistemaDto(IReadOnlyList<RegistroLogSistemaDto> Itens, int Total, int Pagina, int TamanhoPagina,
        DateOnly De, DateOnly Ate, bool Disponivel);
}
