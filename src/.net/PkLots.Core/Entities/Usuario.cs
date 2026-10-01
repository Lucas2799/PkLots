namespace PkLots.Core.Entities
{
    public class Usuario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? EmpresaId { get; set; } // nulo para superadmin e motoristas (contas da plataforma)
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public string NomeCompleto { get; set; } = string.Empty;
        public string Perfil { get; set; } = "motorista"; // superadmin, admin, operador, motorista
        public bool Ativo { get; set; } = true;
        public DateTime? UltimoLogin { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

        public Empresa? Empresa { get; set; }
        public ICollection<UsuarioPatio> Patios { get; set; } = new List<UsuarioPatio>();
        public ICollection<VeiculoMotorista> Veiculos { get; set; } = new List<VeiculoMotorista>();
    }
}
