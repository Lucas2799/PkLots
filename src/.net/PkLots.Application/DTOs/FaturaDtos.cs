namespace PkLots.Application.DTOs
{
    public record FaturaDto(
        Guid Id,
        Guid EmpresaId,
        string NomeEmpresa,
        string Tipo,                // mensalidade, avulsa
        DateOnly? Competencia,
        string Descricao,
        decimal Valor,
        DateOnly Vencimento,
        string Situacao,            // aberta, vencida, paga, cancelada
        int DiasEmAtraso,
        int? PatiosCobrados,
        decimal? PrecoPorPatio,
        string? NomePlano,
        DateTime? PagaEm,
        string? FormaPagamento,
        decimal? ValorPago,
        string? Observacao,
        DateTime? CanceladaEm,
        string? MotivoCancelamento,
        string? PixCopiaECola,      // só em faturas abertas, com a chave do PkLots configurada
        DateTime CriadoEm);

    // Lançamento avulso (ex.: taxa de implantação)
    public record LancarFaturaRequisicao(string Descricao, decimal Valor, DateOnly Vencimento);

    // Baixa manual: data do recebimento (padrão: hoje), forma e valor recebido (padrão: o da fatura)
    public record BaixarFaturaRequisicao(string FormaPagamento, DateOnly? PagaEm = null, decimal? ValorPago = null, string? Observacao = null);

    public record CancelarFaturaRequisicao(string? Motivo);

    public record ConfiguracaoPlataformaDto(string? ChavePix, string? NomeRecebedor, string? CidadeRecebedor);

    public record GeracaoFaturasDto(int Emitidas);
}
