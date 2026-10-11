namespace PkLots.Application.DTOs
{
    public record ResumoDiaDto(int Entradas, int Saidas, decimal Receita, string MediaEstadia);

    public record VeiculoEstacionadoDto(Guid EntradaId, string Placa, DateTime DataHoraEntrada, string Duracao, string? NumeroVaga);

    public record OcupacaoHoraDto(DateTime Hora, int Ocupadas, decimal Percentual);

    public record DashboardDto(
        string NomePatio,
        ResumoDiaDto Hoje,
        ResumoVagasDto Vagas,
        IReadOnlyList<OcupacaoHoraDto> Ocupacao24h,
        IReadOnlyList<VeiculoEstacionadoDto> Estacionados,
        ResumoMensalistasDto? Mensalistas = null);

    // Área do mensalista no app: um contrato por pátio em que a pessoa aceitou o convite
    public record ContratoMensalistaDto(
        Guid Id,
        string NomePatio,
        string NomeEmpresa,
        IReadOnlyList<string> Placas,
        DateOnly? ValidoAte,
        string Situacao,        // em_dia, vencendo, vencido
        int? DiasParaVencer,
        IReadOnlyList<EstadiaAtualDto> Estacionados);

    // Veículo do contrato no pátio agora (valor zero enquanto coberto pela mensalidade)
    public record EstadiaAtualDto(string Placa, DateTime DataHoraEntrada, string Duracao, string? NumeroVaga, decimal ValorEstimado);

    // Pátios visíveis no app, com a disponibilidade atual
    public record PatioDisponibilidadeDto(Guid Id, string Nome, string NomeEmpresa, int Disponiveis, int Total);
}
