namespace PkLots.Application.DTOs
{
    public record ResultadoLprDto(bool Sucesso, string? Placa, decimal Confianca, string Metodo);

    public record RegistrarEntradaRequisicao(string Placa, decimal Confianca = 1m, string? MetodoReconhecimento = "manual", string? UrlFoto = null);

    public record SaidaPorTicketRequisicao(string Codigo);

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
        string Situacao,
        string? Impressao = null); // impresso, falhou, navegador ou null (sem impressão automática)

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
        string? NumeroVaga,
        string NomePatio = "");

    public record HistoricoVeiculoDto(
        Guid EntradaId,
        string Placa,
        DateTime DataHoraEntrada,
        DateTime? DataHoraSaida,
        string? NumeroVaga,
        string? DuracaoFormatada,
        decimal? ValorTotal,
        string Situacao,
        string? NomePatio = null);
}
