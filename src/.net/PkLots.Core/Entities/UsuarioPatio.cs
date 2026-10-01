namespace PkLots.Core.Entities
{
    // Pátios em que um operador pode trabalhar (o admin da empresa acessa todos)
    public class UsuarioPatio
    {
        public Guid UsuarioId { get; set; }
        public Guid PatioId { get; set; }

        public Usuario Usuario { get; set; } = null!;
        public Patio Patio { get; set; } = null!;
    }
}
