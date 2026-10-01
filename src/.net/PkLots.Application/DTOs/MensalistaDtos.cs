namespace PkLots.Application.DTOs
{
    public record MensalistaDto(
        Guid Id,
        string Nome,
        string? Telefone,
        string? Documento,
        string? Observacoes,
        IReadOnlyList<string> Placas,
        DateOnly? ValidoAte,
        string Situacao,        // em_dia, vencendo, vencido, inativo
        int? DiasParaVencer,    // negativo: vencido há N dias
        bool Ativo,
        bool Estacionado);

    public record SalvarMensalistaRequisicao(
        string Nome,
        string? Telefone,
        string? Documento,
        string? Observacoes,
        IReadOnlyList<string> Placas,
        bool Ativo = true);

    // Valor nulo: mensalidade do pátio × meses
    public record RegistrarPagamentoRequisicao(int Meses, decimal? Valor, string FormaPagamento, string? Observacao);

    public record PagamentoDto(
        Guid Id,
        decimal Valor,
        int Meses,
        DateOnly ValidoDe,
        DateOnly ValidoAte,
        string FormaPagamento,
        string? Observacao,
        DateTime PagoEm);

    public record ResumoMensalistasDto(int Ativos, int EmDia, int Vencendo, int Vencidos, int Estacionados, int VagasReservadas, int ReservadasLivres,
        decimal ValorMensalidade = 0);
}
