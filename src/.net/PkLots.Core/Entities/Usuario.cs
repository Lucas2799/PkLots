namespace PkLots.Core.Entities
{
    public class Usuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public string NomeCompleto { get; set; } = string.Empty;
        public string Perfil { get; set; } = "motorista"; // admin, motorista, operador
        public bool Ativo { get; set; } = true;
        public DateTime? UltimoLogin { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Patio Patio { get; set; } = null!;
    }
}
