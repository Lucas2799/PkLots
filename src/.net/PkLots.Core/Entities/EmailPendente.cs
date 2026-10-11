namespace PkLots.Core.Entities
{
    // Fila de saída de e-mails: gravada na mesma transação da regra de negócio e enviada em segundo plano.
    // O conteúdo (que pode ter links de acesso) é apagado depois do envio.
    public class EmailPendente
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Para { get; set; } = string.Empty;
        public string Assunto { get; set; } = string.Empty;
        public string? CorpoHtml { get; set; }
        public string? CorpoTexto { get; set; }
        public int Tentativas { get; set; }
        public DateTime ProximaTentativaEm { get; set; } = DateTime.UtcNow;
        public DateTime? EnviadoEm { get; set; }
        public string? UltimoErro { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }
}
