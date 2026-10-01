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

    public record EstacionamentoAtualDto(
        Guid EntradaId,
        string Placa,
        DateTime DataHoraEntrada,
        string Duracao,
        string? NumeroVaga,
        decimal ValorEstimado,
        string NomePatio = "");

    // Pátios visíveis ao motorista, com a disponibilidade atual
    public record PatioDisponibilidadeDto(Guid Id, string Nome, string NomeEmpresa, int Disponiveis, int Total);

    public record CadastrarVeiculoRequisicao(string Placa, string? Modelo, string? Cor);

    public record VeiculoDto(Guid Id, string Placa, string? Modelo, string? Cor);
}
