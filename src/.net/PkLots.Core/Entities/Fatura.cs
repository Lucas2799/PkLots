namespace PkLots.Core.Entities
{
    // Cobrança do aluguel do PkLots a uma empresa: a mensalidade do plano ou um lançamento avulso
    // (ex.: taxa de implantação). A baixa é manual: o superadmin confirma o recebimento.
    public class Fatura
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EmpresaId { get; set; }
        public string Tipo { get; set; } = "mensalidade"; // mensalidade, avulsa
        public DateOnly? Competencia { get; set; } // 1º dia do mês cobrado (só mensalidade)
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public DateOnly Vencimento { get; set; }
        public string Situacao { get; set; } = "aberta"; // aberta, paga, cancelada

        // Como foi calculada (mensalidade): pátios ativos × preço por pátio no dia da emissão
        public int? PatiosCobrados { get; set; }
        public decimal? PrecoPorPatio { get; set; }
        public string? NomePlano { get; set; }

        public DateTime? PagaEm { get; set; }
        public string? FormaPagamento { get; set; }
        public decimal? ValorPago { get; set; }
        public string? Observacao { get; set; }
        public Guid? BaixadaPorId { get; set; }
        public DateTime? CanceladaEm { get; set; }
        public string? MotivoCancelamento { get; set; }
        public List<string> AvisosEnviados { get; set; } = []; // lembrete, vencimento, bloqueio
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public Empresa Empresa { get; set; } = null!;
    }

    // Dados de recebimento do PkLots (registro único): a chave PIX vai em todas as faturas abertas
    public class ConfiguracaoPlataforma
    {
        public int Id { get; set; } = 1;
        public string? ChavePix { get; set; }
        public string? NomeRecebedor { get; set; }   // até 25 caracteres (padrão do PIX)
        public string? CidadeRecebedor { get; set; } // até 15 caracteres
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    }
}
