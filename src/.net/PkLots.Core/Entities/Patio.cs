namespace PkLots.Core.Entities
{
    // Pátio (tenant): cada filial de estacionamento
    public class Patio
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public int MaximoVagas { get; set; } = 30;
        public decimal ValorHora { get; set; } = 15.00m;
        public int MinutosTolerancia { get; set; } = 0; // saída dentro da tolerância não paga

        // Mensalistas
        public decimal ValorMensalidade { get; set; } = 0m;
        public int VagasReservadasMensalistas { get; set; } = 0; // não ficam disponíveis para avulsos

        // Impressão do ticket de entrada
        public string ModoImpressao { get; set; } = "desativada"; // desativada, rede, navegador
        public string? ImpressoraHost { get; set; } // IP/nome da impressora térmica (modo rede)
        public int ImpressoraPorta { get; set; } = 9100;
        public int LarguraPapelMm { get; set; } = 80; // 58 ou 80
        public bool ImprimirEntradaAutomaticamente { get; set; } = true;
        public bool Ativo { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Empresa Empresa { get; set; } = null!;
        public ICollection<UsuarioPatio> Usuarios { get; set; } = new List<UsuarioPatio>();
        public ICollection<Vaga> Vagas { get; set; } = new List<Vaga>();
        public ICollection<Veiculo> Veiculos { get; set; } = new List<Veiculo>();
        public ICollection<EntradaVeiculo> Entradas { get; set; } = new List<EntradaVeiculo>();
        public ICollection<SaidaVeiculo> Saidas { get; set; } = new List<SaidaVeiculo>();
    }
}
