namespace PkLots.Application.DTOs
{
    // ---------- Relatório do dono (admin da empresa): todos os pátios ou um só ----------

    public record RelatorioEmpresaDto(
        DateOnly De,
        DateOnly Ate,
        string NomeEmpresa,
        string? NomePatio, // nulo: todos os pátios
        IndicadoresPeriodoDto Totais,
        IReadOnlyList<RecebimentoPorFormaDto> PorForma,
        IReadOnlyList<MovimentoDiaDto> PorDia,
        IReadOnlyList<EntradasHoraDto> EntradasPorHora,
        IReadOnlyList<DesempenhoPatioDto> PorPatio,
        IReadOnlyList<DesempenhoOperadorDto> PorOperador,
        IReadOnlyList<CancelamentoRelatorioDto> Cancelamentos,
        MensalistasRelatorioDto Mensalistas);

    public record IndicadoresPeriodoDto(
        int Entradas,
        int Saidas,
        int SaidasCobradas,
        int SaidasIsentas,      // dentro da tolerância
        int SaidasMensalistas,  // cobertas pela mensalidade
        int Cancelamentos,
        decimal ReceitaAvulsos,
        decimal ReceitaMensalidades,
        decimal ReceitaTotal,
        decimal TicketMedio,    // receita de avulsos ÷ saídas cobradas
        string PermanenciaMedia);

    public record MovimentoDiaDto(DateOnly Data, int Entradas, int Saidas, decimal ReceitaAvulsos, decimal ReceitaMensalidades)
    {
        public decimal Receita => ReceitaAvulsos + ReceitaMensalidades;
    }

    // Hora local (0–23) em que os veículos chegam: ajuda a dimensionar a equipe
    public record EntradasHoraDto(int Hora, int Entradas);

    public record DesempenhoPatioDto(
        Guid Id,
        string Nome,
        bool Ativo,
        int Vagas,
        int Entradas,
        int Saidas,
        decimal ReceitaAvulsos,
        decimal ReceitaMensalidades,
        decimal GiroPorVaga); // saídas por vaga por dia

    // Quem recebeu e quanto; diferença de caixa = contado − esperado nos caixas fechados no período
    public record DesempenhoOperadorDto(
        Guid Id,
        string Nome,
        int Saidas,
        int Mensalidades,
        decimal Recebido,
        int CaixasFechados,
        decimal DiferencaCaixa,
        int Cancelamentos);

    public record CancelamentoRelatorioDto(DateTime CanceladaEm, string Placa, string NomePatio, string? CanceladaPor, string? Motivo);

    // Situação atual da carteira + o que mudou no período
    public record MensalistasRelatorioDto(int Ativos, int EmDia, int Vencidos, int Novos, int Pagamentos);
}
