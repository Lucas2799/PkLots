namespace PkLots.Application.DTOs
{
    // Página aberta pelo QR do ticket: só os dados daquela estadia, sem login
    public record EstadiaPublicaDto(
        string NomePatio,
        string Placa,
        string? NumeroVaga,
        string Codigo,
        DateTime DataHoraEntrada,
        string Situacao,           // ativa, concluida, cancelada
        string Duracao,
        decimal Valor,             // ativa: valor se saísse agora; concluída: valor pago
        decimal ValorHora,
        int MinutosTolerancia,
        bool CobertoPorMensalidade,
        DateTime? DataHoraSaida,
        string? FormaPagamento,
        DateTime ConsultadoEm);
}
