namespace PkLots.Application.DTOs
{
    // ---------- Configuração (admin da empresa) ----------

    public record ConfiguracaoPagamentoDto(
        string Provedor,
        string? ChavePix,
        string? NomeRecebedor,
        string? CidadeRecebedor,
        bool AceitaDinheiro,
        bool AceitaPix,
        bool AceitaCartao,
        IReadOnlyList<string> ProvedoresDisponiveis);

    public record AtualizarConfiguracaoPagamentoRequisicao(
        string Provedor,
        string? ChavePix,
        string? NomeRecebedor,
        string? CidadeRecebedor,
        bool AceitaDinheiro,
        bool AceitaPix,
        bool AceitaCartao);

    // ---------- Saída: cobrança e confirmação ----------

    // Identificação do veículo: placa ou código do ticket (um dos dois)
    public record GerarCobrancaRequisicao(string? Placa, string? CodigoTicket);

    public record CobrancaDto(
        Guid Id,
        Guid EntradaId,
        string Placa,
        string? NumeroVaga,
        string NomePatio,
        DateTime DataHoraEntrada,
        DateTime CalculadoEm,
        DateTime ExpiraEm,
        string DuracaoFormatada,
        decimal ValorHora,
        decimal Valor,
        string SituacaoPagamento, // calculado, isento, mensalista
        bool RequerPagamento,
        IReadOnlyList<string> FormasAceitas,
        string? PixCopiaECola,
        string? Mensalista,
        string? Aviso);

    public record ConfirmarSaidaRequisicao(string? FormaPagamento, decimal? ValorRecebido);

    // ---------- Caixa ----------

    public record AbrirCaixaRequisicao(decimal TrocoInicial);

    public record MovimentarCaixaRequisicao(string Tipo, decimal Valor, string? Motivo);

    public record FecharCaixaRequisicao(decimal DinheiroContado, string? Observacao);

    public record MovimentoCaixaDto(Guid Id, string Tipo, decimal Valor, string? Motivo, DateTime CriadoEm);

    public record RecebimentoPorFormaDto(string Forma, int Quantidade, decimal Total);

    public record CaixaDto(
        Guid Id,
        Guid PatioId,
        string NomePatio,
        Guid OperadorId,
        string NomeOperador,
        DateTime AbertoEm,
        DateTime? FechadoEm,
        decimal TrocoInicial,
        IReadOnlyList<RecebimentoPorFormaDto> Recebimentos, // saídas + mensalidades, por forma
        int Saidas,
        decimal TotalRecebido,
        decimal Suprimentos,
        decimal Sangrias,
        decimal DinheiroEsperado,
        decimal? DinheiroContado,
        decimal? Diferenca,
        string? ObservacaoFechamento,
        IReadOnlyList<MovimentoCaixaDto> Movimentos);
}
