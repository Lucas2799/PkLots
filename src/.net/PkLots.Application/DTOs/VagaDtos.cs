namespace PkLots.Application.DTOs
{
    public record VagaDto(
        Guid Id,
        string Numero,
        char Fileira,
        int Indice,
        string Situacao,
        string? PlacaAtual,
        DateTime? OcupadaDesde,
        string? Observacoes);

    public record ResumoVagasDto(int Total, int Disponiveis, int Ocupadas, int Manutencao, DateTime AtualizadoEm);

    public record ManutencaoRequisicao(string? Observacoes);
}
