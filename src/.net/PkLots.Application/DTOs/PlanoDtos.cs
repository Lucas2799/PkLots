namespace PkLots.Application.DTOs
{
    // Recurso que um plano pode liberar (catálogo do sistema)
    public record RecursoPlanoDto(string Codigo, string Nome, string Descricao);

    public record PlanoDto(
        Guid Id,
        string Nome,
        string? Descricao,
        decimal PrecoPorPatio,
        int? MaximoPatios,          // nulo: sem limite
        int? MaximoVagasPorPatio,
        int? MaximoUsuarios,
        IReadOnlyList<string> Recursos,
        int DiasCarencia,
        bool Ativo,
        int Empresas);              // empresas que usam o plano

    public record SalvarPlanoRequisicao(
        string Nome,
        string? Descricao,
        decimal PrecoPorPatio,
        int? MaximoPatios,
        int? MaximoVagasPorPatio,
        int? MaximoUsuarios,
        IReadOnlyList<string>? Recursos,
        int DiasCarencia = 7,
        bool Ativo = true);

    // Visão do superadmin sobre a assinatura de uma empresa
    public record AssinaturaDto(
        Guid EmpresaId,
        Guid PlanoId,
        string NomePlano,
        decimal PrecoPorPatioPlano,
        decimal? PrecoPorPatioNegociado,
        int DiaVencimento,
        DateOnly? TesteAte,
        string? Observacoes,
        int PatiosAtivos,
        decimal ValorMensal);

    public record SalvarAssinaturaRequisicao(
        Guid PlanoId,
        decimal? PrecoPorPatioNegociado,
        int DiaVencimento,
        DateOnly? TesteAte,
        string? Observacoes);

    // Quanto do limite já está em uso (Maximo nulo: sem limite)
    public record UsoLimiteDto(int Usado, int? Maximo);

    // Visão do admin da empresa: o plano contratado, o uso e o que está liberado
    public record MinhaAssinaturaDto(
        string NomePlano,
        string? DescricaoPlano,
        decimal PrecoPorPatio,
        decimal ValorMensal,
        int DiaVencimento,
        DateOnly? TesteAte,
        UsoLimiteDto Patios,
        UsoLimiteDto Usuarios,
        UsoLimiteDto VagasNoMaiorPatio,
        IReadOnlyList<RecursoLiberadoDto> Recursos);

    public record RecursoLiberadoDto(string Codigo, string Nome, bool Liberado);
}
