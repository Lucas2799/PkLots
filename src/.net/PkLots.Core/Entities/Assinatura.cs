namespace PkLots.Core.Entities
{
    // Plano de aluguel do PkLots, criado e editado pelo superadmin: preço por pátio, limites e recursos liberados
    public class Plano
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public decimal PrecoPorPatio { get; set; } // mensal

        // Limites (nulo: sem limite)
        public int? MaximoPatios { get; set; }
        public int? MaximoVagasPorPatio { get; set; }
        public int? MaximoUsuarios { get; set; } // admins + operadores ativos

        public List<string> Recursos { get; set; } = []; // códigos de RecursosPlano
        public int DiasCarencia { get; set; } = 7; // após o vencimento, até o bloqueio parcial
        public bool Ativo { get; set; } = true;   // inativo: não aparece para novas assinaturas
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    }

    // Assinatura da empresa cliente: qual plano usa, quanto paga e quando vence
    public class Assinatura
    {
        public Guid EmpresaId { get; set; }
        public Guid PlanoId { get; set; }
        public decimal? PrecoPorPatioNegociado { get; set; } // substitui o preço do plano (desconto, piloto)
        public int DiaVencimento { get; set; } = 10; // 1 a 28
        public DateOnly? TesteAte { get; set; } // período de teste: sem cobrança até esta data
        public string? Observacoes { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Empresa Empresa { get; set; } = null!;
        public Plano Plano { get; set; } = null!;
    }
}
