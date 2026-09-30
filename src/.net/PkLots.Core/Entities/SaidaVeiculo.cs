namespace PkLots.Core.Entities
{
    public class SaidaVeiculo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public Guid EntradaVeiculoId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public DateTime DataHoraSaida { get; set; }
        public string? UrlFotoSaida { get; set; }
        public int DuracaoSegundos { get; set; }
        public string DuracaoFormatada { get; set; } = string.Empty; // 4h 15m
        public decimal ValorHora { get; set; }
        public decimal ValorTotal { get; set; }
        public string Moeda { get; set; } = "BRL";
        public string SituacaoPagamento { get; set; } = "calculado"; // calculado, pago, erro
        public string? UrlRecibo { get; set; }
        public string Situacao { get; set; } = "concluida";
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public Patio Patio { get; set; } = null!;
        public EntradaVeiculo EntradaVeiculo { get; set; } = null!;
    }
}
