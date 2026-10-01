namespace PkLots.Core.Entities
{
    // Como a empresa recebe: o contrato com o provedor é do dono do pátio; o PkLots só gera e registra as cobranças
    public class ConfiguracaoPagamento
    {
        public Guid EmpresaId { get; set; }
        public string Provedor { get; set; } = "pix_manual"; // pix_manual (chave própria, confirmação pelo operador)
        public string? ChavePix { get; set; }
        public string? NomeRecebedor { get; set; } // até 25 caracteres (padrão do PIX)
        public string? CidadeRecebedor { get; set; } // até 15 caracteres
        public bool AceitaDinheiro { get; set; } = true;
        public bool AceitaPix { get; set; } = true;
        public bool AceitaCartao { get; set; } = true; // maquininha própria do pátio, confirmada pelo operador
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Empresa Empresa { get; set; } = null!;
    }

    // Caixa de um operador num pátio: abre com troco, recebe pagamentos, fecha conferindo o dinheiro
    public class Caixa
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public Guid OperadorId { get; set; }
        public DateTime AbertoEm { get; set; } = DateTime.UtcNow;
        public decimal TrocoInicial { get; set; }
        public DateTime? FechadoEm { get; set; }
        public decimal? DinheiroEsperado { get; set; } // calculado no fechamento
        public decimal? DinheiroContado { get; set; }
        public string? ObservacaoFechamento { get; set; }

        public bool Aberto => FechadoEm is null;

        public Patio Patio { get; set; } = null!;
        public Usuario Operador { get; set; } = null!;
        public ICollection<MovimentoCaixa> Movimentos { get; set; } = new List<MovimentoCaixa>();
    }

    // Sangria (retirada) ou suprimento (reforço de troco)
    public class MovimentoCaixa
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CaixaId { get; set; }
        public string Tipo { get; set; } = "sangria"; // sangria, suprimento
        public decimal Valor { get; set; }
        public string? Motivo { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public Caixa Caixa { get; set; } = null!;
    }

    // Cobrança da saída: congela o valor por alguns minutos (o QR do PIX não muda enquanto o cliente paga)
    public class CobrancaSaida
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public Guid EntradaVeiculoId { get; set; }
        public decimal Valor { get; set; }
        public int DuracaoSegundos { get; set; }
        public string DuracaoFormatada { get; set; } = string.Empty;
        public string SituacaoPagamento { get; set; } = "calculado"; // calculado, isento, mensalista
        public DateTime CalculadoEm { get; set; } = DateTime.UtcNow;
        public DateTime ExpiraEm { get; set; }
        public string TxId { get; set; } = string.Empty; // identificador no PIX (até 25 caracteres)
        public string? PixCopiaECola { get; set; }
        public string Situacao { get; set; } = "pendente"; // pendente, paga, cancelada
        public string? Origem { get; set; } // placa, ticket, imagem
        public string? Mensalista { get; set; } // nome, quando a placa é de mensalista
        public string? Aviso { get; set; }      // ex.: mensalidade vencida, cobrado como avulso

        public Patio Patio { get; set; } = null!;
        public EntradaVeiculo Entrada { get; set; } = null!;
    }
}
