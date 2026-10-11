namespace PkLots.Core.Entities
{
    // Link enviado por e-mail (convite de mensalista, redefinição de senha). Só o hash fica no banco:
    // quem lê o banco não consegue montar o link e tomar a conta.
    public class TokenUsuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Tipo { get; set; } = string.Empty; // convite_mensalista, redefinir_senha
        public string HashToken { get; set; } = string.Empty; // SHA-256 em hexadecimal
        public string Email { get; set; } = string.Empty;
        public Guid? UsuarioId { get; set; }
        public Guid? MensalistaId { get; set; }
        public DateTime ExpiraEm { get; set; }
        public DateTime? UsadoEm { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public Mensalista? Mensalista { get; set; }
    }
}
