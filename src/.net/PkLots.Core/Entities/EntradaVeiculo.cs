namespace PkLots.Core.Entities
{
    public class EntradaVeiculo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public Guid? VeiculoId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public Guid? VagaId { get; set; }
        public DateTime DataHoraEntrada { get; set; }
        public string? UrlFotoEntrada { get; set; }
        public string? MetodoReconhecimento { get; set; } // easyocr, claude_vision
        public decimal Confianca { get; set; } // 0.95
        public string Situacao { get; set; } = "ativa"; // ativa, concluida, cancelada, erro
        public string? CodigoTicket { get; set; } // código impresso no ticket/QR (ex.: K7M2Q9XD), usado na saída por ticket
        public Guid? MensalistaId { get; set; } // preenchido quando entrou como mensalista em dia (usa as vagas reservadas)
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        // Cancelamento (placa digitada errada, veículo que saiu sem passar pelo sistema etc.)
        public DateTime? CanceladaEm { get; set; }
        public Guid? CanceladaPorId { get; set; }
        public string? MotivoCancelamento { get; set; }

        public Patio Patio { get; set; } = null!;
        public Veiculo? Veiculo { get; set; }
        public Vaga? Vaga { get; set; }
        public Mensalista? Mensalista { get; set; }
    }
}
