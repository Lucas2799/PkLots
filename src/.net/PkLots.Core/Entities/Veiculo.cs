namespace PkLots.Core.Entities
{
    public class Veiculo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatioId { get; set; }
        public Guid? UsuarioId { get; set; } // motorista dono do veículo (opcional)
        public string Placa { get; set; } = string.Empty; // ABC1234
        public string? Cor { get; set; }
        public string? Modelo { get; set; }
        public string? NomeProprietario { get; set; }
        public string? Telefone { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Patio Patio { get; set; } = null!;
        public Usuario? Usuario { get; set; }
    }
}
