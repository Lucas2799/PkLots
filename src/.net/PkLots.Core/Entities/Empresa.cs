namespace PkLots.Core.Entities
{
    // Empresa cliente da plataforma (tenant): tem vários pátios e seus próprios usuários
    public class Empresa
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string? Documento { get; set; } // CNPJ/CPF (opcional)
        public bool Ativa { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public ICollection<Patio> Patios { get; set; } = new List<Patio>();
        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
