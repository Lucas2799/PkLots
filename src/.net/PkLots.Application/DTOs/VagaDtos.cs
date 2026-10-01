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

    // ReservadasLivres: vagas reservadas a mensalistas ainda não usadas (não servem a avulsos)
    public record ResumoVagasDto(int Total, int Disponiveis, int Ocupadas, int Manutencao, DateTime AtualizadoEm, int ReservadasLivres = 0)
    {
        public int DisponiveisAvulsos => Math.Max(0, Disponiveis - ReservadasLivres);
    }

    public record ManutencaoRequisicao(string? Observacoes);
}
