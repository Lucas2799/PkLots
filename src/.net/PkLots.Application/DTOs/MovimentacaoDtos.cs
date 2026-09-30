namespace PkLots.Application.DTOs
{
    public record ResultadoLprDto(bool Sucesso, string? Placa, decimal Confianca, string Metodo);

    public record RegistrarEntradaRequisicao(string Placa, decimal Confianca = 1m, string? MetodoReconhecimento = "manual", string? UrlFoto = null);

    public record RegistrarSaidaRequisicao(string Placa, decimal Confianca = 1m, string? UrlFoto = null);

    public record EntradaDto(
        Guid Id,
        string Placa,
        DateTime DataHoraEntrada,
        string? UrlFotoEntrada,
        string? MetodoReconhecimento,
        decimal Confianca,
        Guid? VagaId,
        string? NumeroVaga,
        string Situacao);

    public record SaidaDto(
        Guid Id,
        Guid EntradaVeiculoId,
        string Placa,
        DateTime DataHoraEntrada,
        DateTime DataHoraSaida,
        string? UrlFotoSaida,
        int DuracaoSegundos,
        string DuracaoFormatada,
        decimal DuracaoHoras,
        decimal ValorHora,
        decimal ValorTotal,
        string Moeda,
        string SituacaoPagamento,
        string? NumeroVaga);

    public record HistoricoVeiculoDto(
        Guid EntradaId,
        string Placa,
        DateTime DataHoraEntrada,
        DateTime? DataHoraSaida,
        string? NumeroVaga,
        string? DuracaoFormatada,
        decimal? ValorTotal,
        string Situacao);
}
