namespace PkLots.Core.Entities
{
    // Cliente mensal de um pátio: estaciona sem cobrança enquanto a mensalidade estiver em dia
    public class Mensalista
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Telefone { get; set; }
        public string? Documento { get; set; } // CPF/CNPJ (opcional)
        public string? Observacoes { get; set; }
        public DateOnly? ValidoAte { get; set; } // último dia coberto pelo pagamento (nulo: nunca pagou)
        public bool Ativo { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Patio Patio { get; set; } = null!;
        public ICollection<MensalistaPlaca> Placas { get; set; } = new List<MensalistaPlaca>();
        public ICollection<PagamentoMensalidade> Pagamentos { get; set; } = new List<PagamentoMensalidade>();
    }

    public class MensalistaPlaca
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MensalistaId { get; set; }
        public Guid PatioId { get; set; } // repetido para garantir placa única por pátio
        public string Placa { get; set; } = string.Empty;

        public Mensalista Mensalista { get; set; } = null!;
    }

    // Pagamento registrado manualmente (dinheiro, PIX, cartão...): estende a validade
    public class PagamentoMensalidade
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MensalistaId { get; set; }
        public Guid PatioId { get; set; }
        public decimal Valor { get; set; }
        public int Meses { get; set; } = 1;
        public DateOnly ValidoDe { get; set; }
        public DateOnly ValidoAte { get; set; }
        public string FormaPagamento { get; set; } = "dinheiro"; // dinheiro, pix, cartao, outro
        public string? Observacao { get; set; }
        public Guid? RegistradoPorId { get; set; }
        public DateTime PagoEm { get; set; } = DateTime.UtcNow;

        public Mensalista Mensalista { get; set; } = null!;
        public Patio Patio { get; set; } = null!;
    }
}
