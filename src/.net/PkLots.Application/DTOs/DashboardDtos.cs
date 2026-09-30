namespace PkLots.Application.DTOs
{
    public record ResumoDiaDto(int Entradas, int Saidas, decimal Receita, string MediaEstadia);

    public record VeiculoEstacionadoDto(string Placa, DateTime DataHoraEntrada, string Duracao, string? NumeroVaga);

    public record OcupacaoHoraDto(DateTime Hora, int Ocupadas, decimal Percentual);

    public record DashboardDto(
        string NomePatio,
        ResumoDiaDto Hoje,
        ResumoVagasDto Vagas,
        IReadOnlyList<OcupacaoHoraDto> Ocupacao24h,
        IReadOnlyList<VeiculoEstacionadoDto> Estacionados);

    public record EstacionamentoAtualDto(
        Guid EntradaId,
        string Placa,
        DateTime DataHoraEntrada,
        string Duracao,
        string? NumeroVaga,
        decimal ValorEstimado);

    public record CadastrarVeiculoRequisicao(string Placa, string? Modelo, string? Cor);

    public record VeiculoDto(Guid Id, string Placa, string? Modelo, string? Cor);
}
