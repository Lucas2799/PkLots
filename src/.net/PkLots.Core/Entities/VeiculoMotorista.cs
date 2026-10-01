namespace PkLots.Core.Entities
{
    // Placa vinculada à conta de um motorista (vale em qualquer pátio da plataforma)
    public class VeiculoMotorista
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UsuarioId { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string? Modelo { get; set; }
        public string? Cor { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Usuario Usuario { get; set; } = null!;
    }
}
