namespace PkLots.Application.DTOs
{
    public record ConfiguracaoImpressaoDto(
        string Modo,
        string? ImpressoraHost,
        int ImpressoraPorta,
        int LarguraPapelMm,
        bool ImprimirEntradaAutomaticamente);

    // Dados do ticket de entrada (impressora de rede ou PDF no navegador)
    public record TicketEntradaDto(
        Guid EntradaId,
        string Codigo,
        string NomePatio,
        string Placa,
        string? NumeroVaga,
        DateTime DataHoraEntrada,
        decimal ValorHora,
        int MinutosTolerancia,
        int LarguraPapelMm,
        string ConteudoQrCode,
        string? DescricaoPreco = null); // tabela de preço do pátio, em texto
}
