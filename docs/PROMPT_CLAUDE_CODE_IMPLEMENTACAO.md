# 🤖 PROMPT PARA CLAUDE CODE - Implementação PkLots MVP

**Use este prompt no Claude Code para implementar tudo baseado na estrutura que você já tem!**

---

## 📋 INSTRUÇÃO PRINCIPAL

Copie TUDO abaixo (do "---" até o final) e cole NO CHAT DO CLAUDE CODE:

---

Você é um expert em .NET, Python, Flutter e DevOps. Sua tarefa é **implementar o PkLots MVP EXATAMENTE como descrito no DESCRITIVO_MVP_PKLOTS.md**, usando a estrutura de pastas que já existe no projeto.

Aqui está a estrutura que você tem:
```
PkLots/
├── .vscode/
├── docker/
│   ├── Dockerfile.api
│   ├── Dockerfile.lpr
│   └── nginx.conf
├── docs/
│   └── PkLots.odt
├── .net/
│   ├── PkLots.Api/
│   ├── PkLots.Application/
│   ├── PkLots.Core/
│   ├── PkLots.Infrastructure/
│   ├── PkLots.LprEngine/
│   └── PkLots.sln
├── flutter/
├── python/
│   └── requirements.txt
├── tests/
│   ├── PkLots.Api.Tests/
│   └── PkLots.Infrastructure.Tests/
├── .gitignore
└── README.md
```

## 🎯 SIGA ESTE PLANO (NESTA ORDEM)

### FASE 1: PREPARAR PROJETOS .NET

#### 1.1 Verificar/Criar Solução
```bash
cd .net
dotnet sln list
# Se não existir .sln, criar:
dotnet new sln --name PkLots
```

#### 1.2 Verificar/Criar Projetos
```bash
# Se não existirem, criar:
dotnet new classlib --name PkLots.Core --framework net8.0
dotnet new classlib --name PkLots.Infrastructure --framework net8.0
dotnet new classlib --name PkLots.Application --framework net8.0
dotnet new web --name PkLots.Api --framework net8.0

# Adicionar à solução:
dotnet sln PkLots.sln add PkLots.Core/PkLots.Core.csproj
dotnet sln PkLots.sln add PkLots.Infrastructure/PkLots.Infrastructure.csproj
dotnet sln PkLots.sln add PkLots.Application/PkLots.Application.csproj
dotnet sln PkLots.sln add PkLots.Api/PkLots.Api.csproj
```

#### 1.3 Adicionar Referências Entre Projetos
```bash
cd PkLots.Api
dotnet add reference ../PkLots.Core/PkLots.Core.csproj
dotnet add reference ../PkLots.Infrastructure/PkLots.Infrastructure.csproj
dotnet add reference ../PkLots.Application/PkLots.Application.csproj

cd ../PkLots.Infrastructure
dotnet add reference ../PkLots.Core/PkLots.Core.csproj

cd ../PkLots.Application
dotnet add reference ../PkLots.Core/PkLots.Core.csproj

cd ..
```

#### 1.4 Adicionar Dependências NuGet
```bash
# PkLots.Infrastructure
cd PkLots.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package StackExchange.Redis --version 2.7.10

# PkLots.Api
cd ../PkLots.Api
dotnet add package Serilog --version 4.0.0
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package MediatR --version 12.1.1
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.0.0
dotnet add package Microsoft.IdentityModel.Tokens --version 7.0.0

cd ..
```

---

### FASE 2: CRIAR ENTIDADES (Domain Layer)

Crie em `PkLots.Core/Entities/`:

#### 2.1 Tenant.cs
```csharp
namespace PkLots.Core.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxSpots { get; set; } = 30;
        public decimal HourlyRate { get; set; } = 15.00m;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<ParkingSpot> ParkingSpots { get; set; } = new List<ParkingSpot>();
        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public ICollection<VehicleEntry> VehicleEntries { get; set; } = new List<VehicleEntry>();
        public ICollection<VehicleExit> VehicleExits { get; set; } = new List<VehicleExit>();
    }
}
```

#### 2.2 User.cs
```csharp
namespace PkLots.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } = "driver"; // admin, driver, operator
        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; }
    }
}
```

#### 2.3 ParkingSpot.cs
```csharp
namespace PkLots.Core.Entities
{
    public class ParkingSpot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string SpotNumber { get; set; } // A1, B5, C10
        public char LineLetter { get; set; } // A, B, C
        public int SpotIndex { get; set; } // 1, 2, 3... 10
        public bool IsAvailable { get; set; } = true;
        public bool IsOccupied { get; set; } = false;
        public Guid? CurrentVehicleId { get; set; }
        public string Status { get; set; } = "available"; // available, occupied, maintenance
        public DateTime? OccupiedSince { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; }
    }
}
```

#### 2.4 Vehicle.cs
```csharp
namespace PkLots.Core.Entities
{
    public class Vehicle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string LicensePlate { get; set; } // ABC1234
        public string Color { get; set; }
        public string Model { get; set; }
        public string OwnerName { get; set; }
        public string Phone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; }
    }
}
```

#### 2.5 VehicleEntry.cs
```csharp
namespace PkLots.Core.Entities
{
    public class VehicleEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid? VehicleId { get; set; }
        public string LicensePlate { get; set; }
        public Guid? ParkingSpotId { get; set; }
        public DateTime EntryTime { get; set; }
        public string EntryPhotoUrl { get; set; }
        public string RecognitionMethod { get; set; } // easyocr, claude_vision
        public decimal Confidence { get; set; } // 0.95
        public string Status { get; set; } = "active"; // active, completed, error
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; }
        public Vehicle Vehicle { get; set; }
        public ParkingSpot ParkingSpot { get; set; }
    }
}
```

#### 2.6 VehicleExit.cs
```csharp
namespace PkLots.Core.Entities
{
    public class VehicleExit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid VehicleEntryId { get; set; }
        public string LicensePlate { get; set; }
        public DateTime ExitTime { get; set; }
        public string ExitPhotoUrl { get; set; }
        public int DurationSeconds { get; set; }
        public string DurationFormatted { get; set; } // 4h 15m
        public decimal HourlyRate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "BRL";
        public string PaymentStatus { get; set; } = "calculated"; // calculated, paid, error
        public string ReceiptUrl { get; set; }
        public string Status { get; set; } = "completed";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; }
        public VehicleEntry VehicleEntry { get; set; }
    }
}
```

#### 2.7 AuditLog.cs
```csharp
namespace PkLots.Core.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Action { get; set; } // entry_success, exit_success, lpr_failed
        public string Status { get; set; } // success, error, warning
        public string LicensePlate { get; set; }
        public Dictionary<string, object> Details { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; }
    }
}
```

---

### FASE 3: CRIAR DbContext

Crie em `PkLots.Infrastructure/Data/PkLotsDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PkLots.Core.Entities;

namespace PkLots.Infrastructure.Data
{
    public class PkLotsDbContext : DbContext
    {
        private readonly string _tenantId;

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ParkingSpot> ParkingSpots { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleEntry> VehicleEntries { get; set; }
        public DbSet<VehicleExit> VehicleExits { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        public PkLotsDbContext(DbContextOptions<PkLotsDbContext> options, IHttpContextAccessor httpContext)
            : base(options)
        {
            _tenantId = httpContext?.HttpContext?.Items["TenantId"]?.ToString() ?? "default-patio";
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Multi-Tenant Filters
            modelBuilder.Entity<ParkingSpot>()
                .HasQueryFilter(p => p.TenantId.ToString() == _tenantId);

            modelBuilder.Entity<User>()
                .HasQueryFilter(u => u.TenantId.ToString() == _tenantId);

            modelBuilder.Entity<Vehicle>()
                .HasQueryFilter(v => v.TenantId.ToString() == _tenantId);

            modelBuilder.Entity<VehicleEntry>()
                .HasQueryFilter(ve => ve.TenantId.ToString() == _tenantId);

            modelBuilder.Entity<VehicleExit>()
                .HasQueryFilter(vx => vx.TenantId.ToString() == _tenantId);

            modelBuilder.Entity<AuditLog>()
                .HasQueryFilter(al => al.TenantId.ToString() == _tenantId);

            // Índices
            modelBuilder.Entity<ParkingSpot>()
                .HasIndex(p => new { p.TenantId, p.IsAvailable });

            modelBuilder.Entity<VehicleEntry>()
                .HasIndex(ve => new { ve.TenantId, ve.EntryTime });

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.LicensePlate);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            // Relacionamentos
            modelBuilder.Entity<User>()
                .HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ParkingSpot>()
                .HasOne(ps => ps.Tenant)
                .WithMany(t => t.ParkingSpots)
                .HasForeignKey(ps => ps.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.Tenant)
                .WithMany(t => t.Vehicles)
                .HasForeignKey(v => v.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VehicleEntry>()
                .HasOne(ve => ve.Tenant)
                .WithMany(t => t.VehicleEntries)
                .HasForeignKey(ve => ve.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VehicleExit>()
                .HasOne(vx => vx.Tenant)
                .WithMany(t => t.VehicleExits)
                .HasForeignKey(vx => vx.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

---

### FASE 4: CRIAR MIGRATIONS

```bash
cd PkLots.Infrastructure
dotnet ef migrations add InitialCreate -c PkLotsDbContext -p . -s ../PkLots.Api

cd ../PkLots.Api
dotnet ef database update -c PkLotsDbContext
```

---

### FASE 5: CRIAR PROGRAM.CS (Minimal API)

Crie em `PkLots.Api/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PkLots.Infrastructure.Data;
using Serilog;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/pklots-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.AddDbContext<PkLotsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

// Health Check
app.MapGet("/health", () => new { status = "healthy" })
    .WithName("Health")
    .WithOpenApi();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PkLotsDbContext>();
    db.Database.Migrate();
}

app.Run();
```

---

### FASE 6: CRIAR APPSETTINGS.JSON

Crie em `PkLots.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=pklots_core;Username=pklots_admin;Password=SenhaForteAqui123!;"
  },
  "Services": {
    "LprServiceUrl": "http://localhost:5000"
  },
  "Jwt": {
    "SecretKey": "sua_chave_secreta_super_segura_minimo_32_caracteres_aleatorios",
    "ExpirationMinutes": 1440,
    "Issuer": "pklots-api",
    "Audience": "pklots-app"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

---

### FASE 7: CRIAR PYTHON LPR ENGINE

Crie em `python/app.py`:

```python
from flask import Flask, request, jsonify
import easyocr
import base64
from io import BytesIO
from PIL import Image
import re

app = Flask(__name__)
reader = easyocr.Reader(['pt'])

def validate_brazilian_plate(text):
    """Valida formato brasileiro: ABC1234"""
    clean = text.replace(" ", "").replace("-", "").upper()
    if re.match(r'^[A-Z]{3}\d{4}$', clean):
        return clean
    return None

@app.route('/v1/lpr/analyze', methods=['POST'])
def analyze_plate():
    try:
        if 'image' not in request.files:
            return {'error': 'image field required'}, 400
        
        image = request.files['image']
        img = Image.open(image)
        
        # OCR
        result = reader.readtext(image, detail=1)
        
        if not result:
            return {'success': False, 'plate': None, 'confidence': 0}, 200
        
        best = max(result, key=lambda x: x[2])
        text = best[1]
        confidence = best[2]
        
        plate = validate_brazilian_plate(text)
        
        return {
            'success': plate is not None,
            'plate': plate,
            'confidence': float(confidence),
            'method': 'easyocr'
        }, 200
        
    except Exception as e:
        return {'error': str(e)}, 500

@app.route('/health', methods=['GET'])
def health():
    return {'status': 'healthy'}, 200

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=False)
```

---

### FASE 8: VERIFICAR BUILD

```bash
cd .net
dotnet build PkLots.sln

# Testar execução
cd PkLots.Api
dotnet run
```

---

### FASE 9: INICIALIZAR GIT

```bash
git add .
git commit -m "init: estrutura inicial PkLots MVP com DbContext, Entities, Python LPR"
```

---

## ✅ CHECKLIST

Após completar TUDO acima:

- [ ] PkLots.sln criada e compilando
- [ ] 6 entidades criadas (Tenant, User, ParkingSpot, Vehicle, VehicleEntry, VehicleExit, AuditLog)
- [ ] DbContext criado com multi-tenant filters
- [ ] Migrations aplicadas (PostgreSQL tem schema)
- [ ] Program.cs criado com Minimal API
- [ ] appsettings.json configurado
- [ ] Python LPR app.py criado
- [ ] requirements.txt atualizado
- [ ] dotnet build executa sem erros
- [ ] Git inicializado

---

## PRÓXIMO PASSO

Depois de completar isso, você pode:
1. Criar Controllers (Entry, Exit, Spots)
2. Implementar Autenticação JWT
3. Criar Flutter app
4. Testar fluxo completo

**Quando estiver pronto, execute EXATAMENTE os comandos acima em sequência!**

