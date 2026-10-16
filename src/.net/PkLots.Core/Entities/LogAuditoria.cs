namespace PkLots.Core.Entities
{
    public class LogAuditoria
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public string Acao { get; set; } = string.Empty; // entrada_sucesso, saida_sucesso, lpr_falhou
        public string Situacao { get; set; } = string.Empty; // sucesso, erro, alerta
        public string? Placa { get; set; }
        public Guid? UsuarioId { get; set; } // quem fez (usuário logado na requisição); nulo: rotina automática
        public Dictionary<string, object> Detalhes { get; set; } = new();
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public Patio Patio { get; set; } = null!;
    }
}
