using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    public class PkLotsDbContext : DbContext, IPkLotsDbContext
    {
        private readonly IHttpContextAccessor? _httpContext;

        // Empresa (tenant) do usuário autenticado (claim empresa_id), lida a cada consulta: o DbContext é criado
        // no início da requisição, antes da autenticação, então não pode guardar o valor no construtor.
        // Nula para superadmin, mensalistas (perfil motorista), requisições anônimas e startup: nesses casos os
        // serviços filtram explicitamente (ex.: mensalista só vê os próprios contratos; link do ticket, só aquela estadia).
        private Guid? _empresaId =>
            Guid.TryParse(_httpContext?.HttpContext?.User.FindFirst(ClaimEmpresa)?.Value, out var id) ? id : null;

        public const string ClaimEmpresa = "empresa_id";

        // Usuário autenticado da requisição (claim sub do JWT)
        private Guid? UsuarioLogadoId
        {
            get
            {
                var usuario = _httpContext?.HttpContext?.User;
                var valor = usuario?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? usuario?.FindFirst("sub")?.Value;
                return Guid.TryParse(valor, out var id) ? id : null;
            }
        }

        // Toda auditoria registra quem fez, sem cada serviço precisar informar
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            CarimbarAuditoria();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            CarimbarAuditoria();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void CarimbarAuditoria()
        {
            if (UsuarioLogadoId is not { } usuarioId)
                return;
            foreach (var registro in ChangeTracker.Entries<LogAuditoria>())
                if (registro.State == EntityState.Added && registro.Entity.UsuarioId is null)
                    registro.Entity.UsuarioId = usuarioId;
        }

        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Patio> Patios { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<UsuarioPatio> UsuariosPatios { get; set; }
        public DbSet<Mensalista> Mensalistas { get; set; }
        public DbSet<MensalistaPlaca> MensalistasPlacas { get; set; }
        public DbSet<PagamentoMensalidade> PagamentosMensalidade { get; set; }
        public DbSet<ConfiguracaoPagamento> ConfiguracoesPagamento { get; set; }
        public DbSet<Caixa> Caixas { get; set; }
        public DbSet<MovimentoCaixa> MovimentosCaixa { get; set; }
        public DbSet<CobrancaSaida> CobrancasSaida { get; set; }
        public DbSet<Vaga> Vagas { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<EntradaVeiculo> EntradasVeiculos { get; set; }
        public DbSet<SaidaVeiculo> SaidasVeiculos { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }
        public DbSet<TokenUsuario> TokensUsuario { get; set; }
        public DbSet<EmailPendente> EmailsPendentes { get; set; }
        public DbSet<Plano> Planos { get; set; }
        public DbSet<Assinatura> Assinaturas { get; set; }
        public DbSet<Fatura> Faturas { get; set; }
        public DbSet<ConfiguracaoPlataforma> ConfiguracoesPlataforma { get; set; }

        public PkLotsDbContext(DbContextOptions<PkLotsDbContext> options, IHttpContextAccessor? httpContext = null)
            : base(options)
        {
            _httpContext = httpContext;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Isolamento entre empresas (multi-tenant): cada empresa só enxerga os próprios dados
            modelBuilder.Entity<Empresa>()
                .HasQueryFilter(e => _empresaId == null || e.Id == _empresaId);

            modelBuilder.Entity<Patio>()
                .HasQueryFilter(p => _empresaId == null || p.EmpresaId == _empresaId);

            modelBuilder.Entity<Usuario>()
                .HasQueryFilter(u => _empresaId == null || u.EmpresaId == _empresaId);

            modelBuilder.Entity<UsuarioPatio>()
                .HasQueryFilter(up => _empresaId == null || up.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<Vaga>()
                .HasQueryFilter(v => _empresaId == null || v.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<Veiculo>()
                .HasQueryFilter(v => _empresaId == null || v.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<EntradaVeiculo>()
                .HasQueryFilter(e => _empresaId == null || e.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<SaidaVeiculo>()
                .HasQueryFilter(s => _empresaId == null || s.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<LogAuditoria>()
                .HasQueryFilter(l => _empresaId == null || l.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<Mensalista>()
                .HasQueryFilter(m => _empresaId == null || m.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<MensalistaPlaca>()
                .HasQueryFilter(mp => _empresaId == null || mp.Mensalista.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<PagamentoMensalidade>()
                .HasQueryFilter(p => _empresaId == null || p.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<ConfiguracaoPagamento>()
                .HasQueryFilter(c => _empresaId == null || c.EmpresaId == _empresaId);

            modelBuilder.Entity<Caixa>()
                .HasQueryFilter(c => _empresaId == null || c.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<MovimentoCaixa>()
                .HasQueryFilter(m => _empresaId == null || m.Caixa.Patio.EmpresaId == _empresaId);

            modelBuilder.Entity<CobrancaSaida>()
                .HasQueryFilter(c => _empresaId == null || c.Patio.EmpresaId == _empresaId);

            // Tokens de e-mail e fila de envio são da plataforma: nunca aparecem para usuários de empresa
            modelBuilder.Entity<TokenUsuario>()
                .HasQueryFilter(t => _empresaId == null);
            modelBuilder.Entity<EmailPendente>()
                .HasQueryFilter(e => _empresaId == null);

            // Índices
            modelBuilder.Entity<Vaga>()
                .HasIndex(v => new { v.PatioId, v.Disponivel });

            modelBuilder.Entity<EntradaVeiculo>()
                .HasIndex(e => new { e.PatioId, e.DataHoraEntrada });
            modelBuilder.Entity<EntradaVeiculo>()
                .Property(e => e.MotivoCancelamento).HasMaxLength(SituacaoEntrada.TamanhoMaximoMotivo);

            modelBuilder.Entity<Veiculo>()
                .HasIndex(v => v.Placa);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<UsuarioPatio>()
                .HasKey(up => new { up.UsuarioId, up.PatioId });

            // Uma placa pertence a um só mensalista por pátio
            modelBuilder.Entity<MensalistaPlaca>()
                .HasIndex(mp => new { mp.PatioId, mp.Placa }).IsUnique();

            modelBuilder.Entity<Mensalista>()
                .HasIndex(m => new { m.PatioId, m.Nome });

            modelBuilder.Entity<PagamentoMensalidade>()
                .HasIndex(p => new { p.MensalistaId, p.PagoEm });

            modelBuilder.Entity<PagamentoMensalidade>()
                .Property(p => p.Valor).HasPrecision(10, 2);

            modelBuilder.Entity<Patio>()
                .Property(p => p.ValorMensalidade).HasPrecision(10, 2);
            modelBuilder.Entity<Patio>()
                .Property(p => p.TabelaPrecoJson).HasColumnType("jsonb");

            // Pagamentos e caixa
            // Planos são da plataforma (sem filtro); a assinatura é da empresa
            modelBuilder.Entity<Assinatura>()
                .HasQueryFilter(a => _empresaId == null || a.EmpresaId == _empresaId);
            modelBuilder.Entity<Assinatura>().HasKey(a => a.EmpresaId);
            modelBuilder.Entity<Assinatura>()
                .HasOne(a => a.Empresa).WithOne().HasForeignKey<Assinatura>(a => a.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Assinatura>()
                .HasOne(a => a.Plano).WithMany().HasForeignKey(a => a.PlanoId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Assinatura>().Property(a => a.PrecoPorPatioNegociado).HasPrecision(10, 2);
            modelBuilder.Entity<Assinatura>().Property(a => a.Observacoes).HasMaxLength(500);
            modelBuilder.Entity<Plano>().Property(p => p.Nome).HasMaxLength(60);
            modelBuilder.Entity<Plano>().Property(p => p.Descricao).HasMaxLength(300);
            modelBuilder.Entity<Plano>().Property(p => p.PrecoPorPatio).HasPrecision(10, 2);
            modelBuilder.Entity<Plano>().HasIndex(p => p.Nome).IsUnique();

            modelBuilder.Entity<Fatura>()
                .HasQueryFilter(f => _empresaId == null || f.EmpresaId == _empresaId);
            modelBuilder.Entity<Fatura>()
                .HasOne(f => f.Empresa).WithMany().HasForeignKey(f => f.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            // Uma mensalidade por empresa e mês
            modelBuilder.Entity<Fatura>()
                .HasIndex(f => new { f.EmpresaId, f.Competencia }).IsUnique().HasFilter($"\"Tipo\" = '{TipoFatura.Mensalidade}'");
            modelBuilder.Entity<Fatura>().HasIndex(f => new { f.Situacao, f.Vencimento });
            modelBuilder.Entity<Fatura>().Property(f => f.Descricao).HasMaxLength(TipoFatura.TamanhoMaximoDescricao);
            modelBuilder.Entity<Fatura>().Property(f => f.Valor).HasPrecision(10, 2);
            modelBuilder.Entity<Fatura>().Property(f => f.ValorPago).HasPrecision(10, 2);
            modelBuilder.Entity<Fatura>().Property(f => f.PrecoPorPatio).HasPrecision(10, 2);
            modelBuilder.Entity<Fatura>().Property(f => f.Observacao).HasMaxLength(300);
            modelBuilder.Entity<Fatura>().Property(f => f.MotivoCancelamento).HasMaxLength(300);
            modelBuilder.Entity<ConfiguracaoPlataforma>().Property(c => c.NomeRecebedor).HasMaxLength(25);
            modelBuilder.Entity<ConfiguracaoPlataforma>().Property(c => c.CidadeRecebedor).HasMaxLength(15);

            modelBuilder.Entity<ConfiguracaoPagamento>().HasKey(c => c.EmpresaId);
            modelBuilder.Entity<ConfiguracaoPagamento>()
                .HasOne(c => c.Empresa).WithOne().HasForeignKey<ConfiguracaoPagamento>(c => c.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ConfiguracaoPagamento>().Property(c => c.NomeRecebedor).HasMaxLength(25);
            modelBuilder.Entity<ConfiguracaoPagamento>().Property(c => c.CidadeRecebedor).HasMaxLength(15);

            // Cada operador tem no máximo um caixa aberto
            modelBuilder.Entity<Caixa>()
                .HasIndex(c => c.OperadorId).IsUnique().HasFilter("\"FechadoEm\" IS NULL");
            modelBuilder.Entity<Caixa>().HasIndex(c => new { c.PatioId, c.AbertoEm });
            modelBuilder.Entity<Caixa>().Ignore(c => c.Aberto);
            modelBuilder.Entity<Caixa>().Property(c => c.TrocoInicial).HasPrecision(10, 2);
            modelBuilder.Entity<Caixa>().Property(c => c.DinheiroEsperado).HasPrecision(10, 2);
            modelBuilder.Entity<Caixa>().Property(c => c.DinheiroContado).HasPrecision(10, 2);
            modelBuilder.Entity<Caixa>()
                .HasOne(c => c.Patio).WithMany().HasForeignKey(c => c.PatioId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Caixa>()
                .HasOne(c => c.Operador).WithMany().HasForeignKey(c => c.OperadorId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimentoCaixa>().Property(m => m.Valor).HasPrecision(10, 2);
            modelBuilder.Entity<MovimentoCaixa>()
                .HasOne(m => m.Caixa).WithMany(c => c.Movimentos).HasForeignKey(m => m.CaixaId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CobrancaSaida>().Property(c => c.Valor).HasPrecision(10, 2);
            modelBuilder.Entity<CobrancaSaida>().Property(c => c.TxId).HasMaxLength(25);
            modelBuilder.Entity<CobrancaSaida>().HasIndex(c => new { c.EntradaVeiculoId, c.Situacao });
            modelBuilder.Entity<CobrancaSaida>()
                .HasOne(c => c.Patio).WithMany().HasForeignKey(c => c.PatioId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CobrancaSaida>()
                .HasOne(c => c.Entrada).WithMany().HasForeignKey(c => c.EntradaVeiculoId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SaidaVeiculo>().Property(s => s.ValorRecebido).HasPrecision(10, 2);
            modelBuilder.Entity<SaidaVeiculo>().Property(s => s.Troco).HasPrecision(10, 2);
            modelBuilder.Entity<SaidaVeiculo>().HasIndex(s => s.CaixaId);
            modelBuilder.Entity<PagamentoMensalidade>().HasIndex(p => p.CaixaId);

            modelBuilder.Entity<Vaga>()
                .HasIndex(v => new { v.PatioId, v.Numero }).IsUnique();

            modelBuilder.Entity<Veiculo>()
                .HasIndex(v => new { v.PatioId, v.Placa }).IsUnique();

            // Um veículo só pode ter uma entrada ativa por pátio
            modelBuilder.Entity<EntradaVeiculo>()
                .HasIndex(e => new { e.PatioId, e.Placa })
                .IsUnique()
                .HasFilter($"\"Situacao\" = '{SituacaoEntrada.Ativa}'");

            modelBuilder.Entity<SaidaVeiculo>()
                .HasIndex(s => s.EntradaVeiculoId).IsUnique();

            modelBuilder.Entity<EntradaVeiculo>()
                .HasIndex(e => new { e.PatioId, e.CodigoTicket })
                .IsUnique()
                .HasFilter("\"CodigoTicket\" IS NOT NULL");

            modelBuilder.Entity<EntradaVeiculo>()
                .Property(e => e.CodigoTicket)
                .HasMaxLength(8);

            modelBuilder.Entity<EntradaVeiculo>()
                .HasIndex(e => e.TokenAcesso)
                .IsUnique()
                .HasFilter("\"TokenAcesso\" IS NOT NULL");
            modelBuilder.Entity<EntradaVeiculo>()
                .Property(e => e.TokenAcesso)
                .HasMaxLength(32);

            // Acesso de mensalistas ao app
            modelBuilder.Entity<Mensalista>().Property(m => m.Email).HasMaxLength(254);
            modelBuilder.Entity<Mensalista>().HasIndex(m => m.UsuarioId);
            modelBuilder.Entity<Mensalista>()
                .HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TokenUsuario>().HasIndex(t => t.HashToken).IsUnique();
            modelBuilder.Entity<TokenUsuario>().HasIndex(t => new { t.MensalistaId, t.Tipo });
            modelBuilder.Entity<TokenUsuario>().HasIndex(t => new { t.UsuarioId, t.Tipo });
            modelBuilder.Entity<TokenUsuario>().Property(t => t.Tipo).HasMaxLength(30);
            modelBuilder.Entity<TokenUsuario>().Property(t => t.HashToken).HasMaxLength(64);
            modelBuilder.Entity<TokenUsuario>().Property(t => t.Email).HasMaxLength(254);
            modelBuilder.Entity<TokenUsuario>()
                .HasOne(t => t.Mensalista).WithMany().HasForeignKey(t => t.MensalistaId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TokenUsuario>()
                .HasOne<Usuario>().WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailPendente>().HasIndex(e => new { e.EnviadoEm, e.ProximaTentativaEm });
            modelBuilder.Entity<EmailPendente>().Property(e => e.Para).HasMaxLength(254);
            modelBuilder.Entity<EmailPendente>().Property(e => e.Assunto).HasMaxLength(200);

            modelBuilder.Entity<LogAuditoria>()
                .HasIndex(l => new { l.PatioId, l.CriadoEm });
            modelBuilder.Entity<LogAuditoria>()
                .HasIndex(l => l.CriadoEm); // tela de auditoria da plataforma (todas as empresas)

            // Concorrência otimista na alocação de vagas (coluna de sistema xmin)
            modelBuilder.Entity<Vaga>()
                .Property(v => v.Versao)
                .IsRowVersion();

            // Relacionamentos
            modelBuilder.Entity<Patio>()
                .HasOne(p => p.Empresa)
                .WithMany(e => e.Patios)
                .HasForeignKey(p => p.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Empresa)
                .WithMany(e => e.Usuarios)
                .HasForeignKey(u => u.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UsuarioPatio>()
                .HasOne(up => up.Usuario)
                .WithMany(u => u.Patios)
                .HasForeignKey(up => up.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UsuarioPatio>()
                .HasOne(up => up.Patio)
                .WithMany(p => p.Usuarios)
                .HasForeignKey(up => up.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Mensalista>()
                .HasOne(m => m.Patio)
                .WithMany()
                .HasForeignKey(m => m.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MensalistaPlaca>()
                .HasOne(mp => mp.Mensalista)
                .WithMany(m => m.Placas)
                .HasForeignKey(mp => mp.MensalistaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PagamentoMensalidade>()
                .HasOne(p => p.Mensalista)
                .WithMany(m => m.Pagamentos)
                .HasForeignKey(p => p.MensalistaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PagamentoMensalidade>()
                .HasOne(p => p.Patio)
                .WithMany()
                .HasForeignKey(p => p.PatioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EntradaVeiculo>()
                .HasOne(e => e.Mensalista)
                .WithMany()
                .HasForeignKey(e => e.MensalistaId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Vaga>()
                .HasOne(v => v.Patio)
                .WithMany(p => p.Vagas)
                .HasForeignKey(v => v.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Veiculo>()
                .HasOne(v => v.Patio)
                .WithMany(p => p.Veiculos)
                .HasForeignKey(v => v.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EntradaVeiculo>()
                .HasOne(e => e.Patio)
                .WithMany(p => p.Entradas)
                .HasForeignKey(e => e.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SaidaVeiculo>()
                .HasOne(s => s.Patio)
                .WithMany(p => p.Saidas)
                .HasForeignKey(s => s.PatioId)
                .OnDelete(DeleteBehavior.Cascade);

            // LogAuditoria.Detalhes armazenado como jsonb
            modelBuilder.Entity<LogAuditoria>()
                .Property(l => l.Detalhes)
                .HasColumnType("jsonb")
                .HasConversion(
                    d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                    s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>(),
                    new ValueComparer<Dictionary<string, object>>(
                        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                        d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
                        d => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(d, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
        }
    }
}
