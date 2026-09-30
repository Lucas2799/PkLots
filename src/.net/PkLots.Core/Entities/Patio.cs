namespace PkLots.Core.Entities
{
    // Pátio (tenant): cada filial de estacionamento
    public class Patio
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public int MaximoVagas { get; set; } = 30;
        public decimal ValorHora { get; set; } = 15.00m;
        public bool Ativo { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public ICollection<Vaga> Vagas { get; set; } = new List<Vaga>();
        public ICollection<Veiculo> Veiculos { get; set; } = new List<Veiculo>();
        public ICollection<EntradaVeiculo> Entradas { get; set; } = new List<EntradaVeiculo>();
        public ICollection<SaidaVeiculo> Saidas { get; set; } = new List<SaidaVeiculo>();
    }
}
