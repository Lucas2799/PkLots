using PkLots.Application.Servicos;

namespace PkLots.Application.DTOs
{
    // Tabela de preço do pátio (FimDeSemana nulo: sábado e domingo seguem a semana)
    public record TabelaPrecoDto(RegrasPreco Semana, RegrasPreco? FimDeSemana, int MinutosTolerancia, string Descricao);

    // MinutosTolerancia nulo: mantém a tolerância atual do pátio
    public record SalvarTabelaPrecoRequisicao(RegrasPreco Semana, RegrasPreco? FimDeSemana, int? MinutosTolerancia = null);

    // Simula uma estadia com a tabela informada (ainda não salva); sem tolerância informada, usa a do pátio
    public record SimularTarifaRequisicao(RegrasPreco Semana, RegrasPreco? FimDeSemana, DateTime Entrada, DateTime Saida,
        int? MinutosTolerancia = null);

    public record SimulacaoTarifaDto(decimal Valor, string Duracao, bool Isento, bool FimDeSemana, string? Detalhamento);
}
