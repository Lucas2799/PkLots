namespace PkLots.Core.Entities
{
    public class Vaga
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public string Numero { get; set; } = string.Empty; // A1, B5, C10
        public char Fileira { get; set; } // A, B, C
        public int Indice { get; set; } // 1, 2, 3... 10
        public bool Disponivel { get; set; } = true;
        public bool Ocupada { get; set; } = false;
        public Guid? VeiculoAtualId { get; set; }
        public string Situacao { get; set; } = "disponivel"; // disponivel, ocupada, manutencao
        public DateTime? OcupadaDesde { get; set; }
        public string? Observacoes { get; set; }
        public bool Ativa { get; set; } = true; // vagas removidas pelo admin ficam inativas (preserva o histórico)
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
        public uint Versao { get; set; } // controle de concorrência (xmin no PostgreSQL)

        public Patio Patio { get; set; } = null!;
    }
}
