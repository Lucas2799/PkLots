# 🅿️ PkLots - Sistema de Gerenciamento de Estacionamentos

![Status](https://img.shields.io/badge/status-development-yellow)
![.NET](https://img.shields.io/badge/.NET-8.0-512bd4)
![Python](https://img.shields.io/badge/Python-3.11-3776ab)
![Flutter](https://img.shields.io/badge/Flutter-latest-02569B)
![License](https://img.shields.io/badge/license-MIT-green)

> **Um sistema completo, moderno e escalável para gerenciamento de estacionamentos com reconhecimento automático de placas via IA, integrando backend robusto, processamento de imagens inteligente e interface multiplataforma**

---

## 📚 ENTENDENDO O PROJETO PKLOTS
 
### O Que É PkLots?
 
**PkLots** é uma solução empresarial para **automatizar completamente o gerenciamento de estacionamentos**. É um software que:
 
1. **Registra entrada de carros automaticamente** - Fotografa a placa, a IA identifica, sistema registra
2. **Controla vagas em tempo real** - Mostra quantas vagas têm livres agora
3. **Registra saída de carros** - Calcula tempo estacionado, emite recibo, autoriza saída
4. **Funciona em múltiplas filiais** - Um sistema gerencia vários pátios simultaneamente
5. **Oferece interface amigável** - App no celular, web, desktop - tudo sincronizado
### O Problema que Resolve
 
**Imagine um cenário hoje, sem PkLots:**
 
```
🚗 Motorista chega no estacionamento
    ↓
🤷 Precisa procurar vaga manualmente
    ↓
❌ Não sabe quantas vagas tem disponíveis
    ↓
🤷 Encontra vaga, estaciona
    ↓
📝 Recebe um tíquete de papel (ou nada)
    ↓
⏰ Fica estacionado, ninguém rastreia
    ↓
🚪 Volta para buscar carro
    ↓
🤷 Perde tíquete? Não sabe qual era seu carro
    ↓
💬 Vai na cabine, precisa falar com atendente
    ↓
⏱️ Atendente digita manualmente quanto tempo passou
    ↓
💰 Calcula valor, cobra, abre cancela
    ↓
😤 Processo lento, propenso a erros, sem rastreamento
```
 
**Com PkLots, o fluxo é:**
 
```
🚗 Motorista chega no estacionamento
    ↓
📸 Câmera fotografa placa automaticamente
    ↓
🤖 IA lê a placa (ABC1234)
    ↓
✅ Sistema registra entrada automática (sem intervenção humana)
    ↓
📱 App mostra vagas disponíveis em TEMPO REAL
    ↓
🎯 Motorista vai direto para vaga
    ↓
⏱️ Sistema rastreia tempo desde entrada
    ↓
🚪 Motorista volta para seu carro
    ↓
📸 Câmera fotografa placa novamente
    ↓
🤖 IA confirma que é o mesmo carro
    ↓
✅ Sistema registra saída automática
    ↓
💰 Calcula automaticamente: entrada 10:30, saída 14:45 = 4h15m
    ↓
💳 Gera recibo digital automaticamente
    ↓
🚪 Cancela abre, saída autorizada
    ↓
😊 Processo rápido, automático, seguro, rastreado
```
 
---
 
## 🎯 Casos de Uso Reais
 
### 1️⃣ Shopping Center
 
```
Cenário: Estacionamento com 500 vagas
 
ANTES (sem PkLots):
- Atendentes precisavam contar vagas manualmente
- Fila na entrada esperando por tíquete de papel
- Fila na saída esperando por atendente fazer conta
- Perda de tíquetes = multas adicionais
- Sem rastreamento de horários cheios
- Sem dados para planejamento
 
DEPOIS (com PkLots):
- App mostra 237 vagas disponíveis em TEMPO REAL
- Motorista entra sozinho, câmera registra
- Motorista sai sozinho, sistema calcula
- Recibo digital no app
- Histórico completo de ocupação por hora
- Dados para saber quando está lotado
- Relatórios de receita automáticos
- Atendentes agora ajudam em problemas, não ficam digitando
 
ROI: 2 atendentes economizados = R$ 5.000/mês
Tempo médio saída: 2 min (antes) → 10 seg (depois)
```
 
### 2️⃣ Hospital
 
```
Cenário: Estacionamento com 300 vagas, visitantes + funcionários
 
ANTES:
- Visitantes perdidos procurando vaga
- Funcionários sem prioridade
- Emergências esperando para entrar
- Sem controle de quem estacionou
 
DEPOIS (com PkLots):
- Placa do carro rastreada desde entrada
- Prioridade para ambulâncias (detecta tipos de vaga)
- Segurança: sabe exatamente quem estacionou
- Histórico de cada veículo
- Integração com sistema hospitalar para agilizar entrada
 
BENEFÍCIO: Emergência ganha tempo (cancela abre instantâneo)
```
 
### 3️⃣ Pátio de Construção / Logística
 
```
Cenário: 200 vagas, caminhões e carros, múltiplas entradas
 
ANTES:
- Porteiro precisa anotar cada veículo que entra
- Sem controle de horários
- Impossível auditar
 
DEPOIS (com PkLots):
- Cada veículo fotografado automaticamente
- Entrada: ABC1234 entrada às 08:30
- Saída: ABC1234 saída às 17:15
- Relatório completo para auditoria
- Integração com software de frota da empresa
 
BENEFÍCIO: Conformidade legal, rastreamento de custos
```
 
### 4️⃣ Locadora de Veículos
 
```
Cenário: 150 vagas com veículos de aluguel
 
ANTES:
- Conferir manualmente qual carro saiu
- Sem saber se foi danificado na saída
- Sem fotos de comparação
 
DEPOIS (com PkLots):
- Foto de entrada: carro em perfeito estado
- Foto de saída: detecta riscos, amassados, vidros quebrados
- IA compara imagens
- Alerta automático se diferenças detectadas
- Documentação automática para cobrar danos
 
BENEFÍCIO: Reduz fraudes, documentação automatizada
```
 
---
 
## 🏗️ Como Funciona Tecnicamente
 
### Fluxo de um Carro Entrando
 
```
┌─────────────────────────────────────────────────────┐
│ 1. CARRO CHEGA NA ENTRADA                          │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 2. CÂMERA CAPTURA FOTO DA PLACA                    │
│    (Qualidade: 1080p, com iluminação)              │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 3. IMAGEM ENVIADA PARA PYTHON (LPR ENGINE)        │
│    POST http://pklots-lpr:5000/v1/lpr/analyze     │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 4. IA PROCESSA (EasyOCR + Claude Vision)           │
│    - Detecta região de placa                        │
│    - Extrai números/letras                          │
│    - Valida formato brasileiro (ABC1234)            │
│    - Retorna com confiança (95%)                    │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 5. API .NET RECEBE RESULTADO                       │
│    POST /api/v1/entries                            │
│    Payload: {                                       │
│      "licensePlate": "ABC1234",                    │
│      "photoUrl": "s3://bucket/abc1234_entry.jpg"   │
│      "confidence": 0.95                            │
│    }                                                │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 6. VALIDAÇÕES NO BANCO DE DADOS                    │
│    - Carro já existe no banco?                      │
│    - Se não, cria registro novo                     │
│    - Registra entrada com timestamp                 │
│    - Atualiza vagas: 500 → 499 livres               │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 7. ATUALIZA CACHE (Redis)                          │
│    Key: "tenant:123:spots:available"               │
│    Value: {                                         │
│      "available": 499,                              │
│      "occupied": 1,                                 │
│      "total": 500                                   │
│    }                                                │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 8. APLICAÇÃO FLUTTER ATUALIZA (WebSocket/Poll)    │
│    Dashboard mostra: "499 vagas disponíveis"        │
│    Motorista consegue ver em tempo real             │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 9. CANCELA SE ABRE AUTOMATICAMENTE                 │
│    Motores recebem sinal de liberação               │
│    Carro entra no pátio                            │
└─────────────────────────────────────────────────────┘
```
 
### Fluxo de um Carro Saindo
 
```
┌─────────────────────────────────────────────────────┐
│ 1. CARRO VAI EMBORA, CHEGA NA CANCELA DE SAÍDA    │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 2. CÂMERA CAPTURA FOTO NOVAMENTE                   │
│    (Segunda foto: para auditoria)                   │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 3. IA LÊ PLACA (Mesmo processo de entrada)        │
│    Resultado: ABC1234                              │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 4. API .NET PROCESSA SAÍDA                         │
│    POST /api/v1/exits/{entryId}                    │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 5. CÁLCULOS AUTOMÁTICOS                            │
│    - Entrada: 10:30 (do banco)                      │
│    - Saída: 14:45 (agora)                          │
│    - Duração: 4 horas 15 minutos                    │
│    - Taxa: R$ 15/hora = R$ 63,75                   │
│    - Gera recibo automático                         │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 6. ATUALIZA VAGAS (Redis + Banco)                  │
│    - Vaga liberada: 499 → 500 vagas livres          │
│    - Dashboard atualiza em tempo real               │
└──────────────┬──────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────┐
│ 7. AUTORIZA SAÍDA                                  │
│    - Se pagamento confirmado: abre cancela         │
│    - Carro sai                                      │
│    - Registra saída no histórico                    │
└─────────────────────────────────────────────────────┘
```
 
---
 
## 👥 Quem Usa PkLots?
 
### 🏪 Proprietários de Estacionamentos
```
Benefícios:
✅ Aumenta receita (menos perda de dados)
✅ Reduz custos (menos atendentes)
✅ Dados para decisões (horários cheios, sazonalidade)
✅ Segurança (rastreamento de veículos)
```
 
### 🚗 Motoristas / Clientes
```
Benefícios:
✅ Entrada/saída rápida (não precisa falar com ninguém)
✅ Sabe quantas vagas têm antes de entrar
✅ Recibo digital automático
✅ Sem risco de perder tíquete
```
 
### 👔 Gerentes de Facilities
```
Benefícios:
✅ Relatórios detalhados de ocupação
✅ Alertas de problemas
✅ Rastreamento de receita
✅ Dados para planejamento (precisa de mais vagas?)
```
 
### 🔒 Segurança / Auditor
```
Benefícios:
✅ Histórico completo (quem entrou, quando, placa)
✅ Fotos de todos os carros
✅ Rastreamento em tempo real
✅ Integração com sistema de segurança
```
 
---

## 🏗️ Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                    Flutter App (Web/Mobile/Desktop)          │
│              (Login, Dashboard, Check-in/out, Histórico)    │
└──────────────────────┬──────────────────────────────────────┘
                       │ (HTTPS/HTTP)
┌──────────────────────▼──────────────────────────────────────┐
│              Nginx - API Gateway / Reverse Proxy             │
│                   (CORS, Rate Limiting)                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
       ┌───────────────┼───────────────┐
       │               │               │
       ▼               ▼               ▼
  ┌─────────┐   ┌──────────┐   ┌──────────────┐
  │  .NET   │   │ Python   │   │  PostgreSQL  │
  │   API   │   │   LPR    │   │   Database   │
  │ (8080)  │   │  (5000)  │   │   (5432)     │
  └────┬────┘   └────┬─────┘   └──────┬───────┘
       │             │                 │
       └─────┬───────┴────────────┬────┘
             │                    │
             ▼                    ▼
        ┌─────────────────┐   ┌─────────────┐
        │  Redis Cache    │   │   Volumes   │
        │  (Vagas)        │   │  Postgres   │
        │  (6379)         │   │   Redis     │
        └─────────────────┘   └─────────────┘
```

### Componentes

| Camada | Tecnologia | Responsabilidade |
|--------|-----------|------------------|
| **Frontend** | Flutter 3.x | UI multiplataforma (web, mobile, desktop) |
| **API Gateway** | Nginx + YARP | Roteamento, CORS, rate limiting |
| **Backend** | .NET 8 (Minimal API) | Lógica de negócio, autenticação, multi-tenant |
| **IA/Visão** | Python + OpenCV/EasyOCR | Reconhecimento de placas (LPR) |
| **Cache** | Redis 7 | Ocupação de vagas em tempo real |
| **Banco de Dados** | PostgreSQL 16 | Persistência de dados |
| **Orquestração** | Podman Compose | Containerização e orchestração local |

---

## 🛠️ Stack Tecnológico

### Backend (.NET)

```csharp
// Arquitetura Modular em 4 camadas
PkLots.Core              → Domain (Entities, Enums, Interfaces)
PkLots.Infrastructure    → Data Access (EF Core, Repositories, Services)
PkLots.Application       → Business Logic (UseCases, DTOs, MediatR)
PkLots.Api              → REST API (Controllers, Middleware, Configuration)
```

**Dependências principais:**
- `Microsoft.EntityFrameworkCore` 8.0 - ORM
- `Microsoft.EntityFrameworkCore.PostgreSQL` 8.0 - Driver PostgreSQL
- `StackExchange.Redis` 2.7.10 - Client Redis
- `MediatR` 12.1.1 - CQRS Pattern
- `Serilog` 4.0 - Logging estruturado
- `System.IdentityModel.Tokens.Jwt` - JWT Auth

### Backend (Python)

```python
# Microsserviço de Visão Computacional (LPR)
- Flask 3.0 - Framework web
- OpenCV 4.8 - Processamento de imagens
- EasyOCR 1.7 - Reconhecimento de placas
- NumPy 1.24 - Computação numérica
- Pydantic 2.3 - Validação de dados
```

### Frontend (Flutter)

```yaml
# Aplicação multiplataforma
dependencies:
  flutter:
    sdk: flutter
  http: ^1.1.0           # HTTP client
  provider: ^6.0.0       # State management
  jwt_decoder: ^2.0.0    # JWT parsing
  camera: ^0.10.0        # Câmera para check-in
  image_picker: ^1.0.0   # Seleção de imagens
```

### Infraestrutura

```yaml
services:
  postgres:16-alpine     # Banco de dados relacional
  redis:7-alpine         # Cache + Estado rápido
  nginx:alpine           # Reverse proxy
  podman                 # Container runtime (Fedora)
```

---

## 📋 Requisitos

### Sistema Operacional
- ✅ **Fedora** 38+ (recomendado)
- ✅ Ubuntu 22.04+ (compatível)
- ✅ Debian 12+ (compatível)
- ⚠️ Windows com WSL2 + Podman (não recomendado para dev)

### Ferramentas Obrigatórias

| Ferramenta | Versão Mínima | Comando de Verificação |
|-----------|---------------|----------------------|
| **Podman** | 4.4+ | `podman --version` |
| **.NET SDK** | 8.0+ | `dotnet --version` |
| **Git** | 2.30+ | `git --version` |
| **Python** | 3.10+ | `python3 --version` |

### Ferramentas Opcionais

- Docker (substituir Podman)
- Postman/Insomnia (testar API)
- DBeaver (gerenciar PostgreSQL)
- VS Code (editor)
- Rider (IDE .NET premium)

### Hardware Mínimo Recomendado

| Recurso | Mínimo | Recomendado |
|---------|--------|------------|
| CPU | 2 cores | 4+ cores |
| RAM | 4GB | 8GB+ |
| Disco | 10GB livres | 50GB+ |
| Internet | 2Mbps | 10Mbps+ |

---

## 🚀 Quick Start (5 minutos)

### 1️⃣ Pré-requisitos

```bash
# Verificar instalações
podman --version        # ✅ 4.4+
dotnet --version        # ✅ 8.0+
git --version          # ✅ 2.30+
python3 --version      # ✅ 3.10+

# Se faltar podman-compose
sudo dnf install podman-compose
```

### 2️⃣ Clonar/Preparar Projeto

```bash
# Opção A: Clone (quando repo existir)
git clone https://github.com/seu-usuario/pklots.git
cd PkLots

# Opção B: Criar do zero com Claude Code
# → Copie o PROMPT_COPIAR_COLAR.md para Claude Code
# → Deixe executar até terminar (5 min)
```

### 3️⃣ Iniciar Infraestrutura

```bash
# Subir PostgreSQL + Redis
podman-compose up -d pklots-db pklots-cache

# Verificar
podman-compose ps
podman exec -it pklots-postgres psql -U pklots_admin -d pklots_core -c "SELECT NOW();"
```

### 4️⃣ Compilar .NET

```bash
cd src
dotnet build PkLots.sln
dotnet run --project PkLots.Api
```

**Resultado esperado:**
```
Now listening on: http://localhost:8080
Press CTRL+C to exit
```

### 5️⃣ Testar API

```bash
# Terminal novo
curl -X GET http://localhost:8080/health

# Resultado
{"status":"healthy"}
```

---

## 📁 Estrutura de Pastas

```
PkLots/
│
├── src/
│   ├── PkLots.Core/                      # Domain Layer
│   │   ├── Entities/
│   │   │   ├── Tenant.cs
│   │   │   ├── User.cs
│   │   │   ├── ParkingSpot.cs
│   │   │   ├── Vehicle.cs
│   │   │   ├── VehicleEntry.cs
│   │   │   └── VehicleExit.cs
│   │   ├── Enums/
│   │   │   └── UserRole.cs
│   │   └── Interfaces/
│   │       └── IRepository.cs
│   │
│   ├── PkLots.Infrastructure/            # Data Access Layer
│   │   ├── Data/
│   │   │   ├── PkLotsDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Repositories/
│   │   │   ├── TenantRepository.cs
│   │   │   ├── UserRepository.cs
│   │   │   └── ParkingSpotRepository.cs
│   │   └── Services/
│   │       ├── AuthService.cs
│   │       ├── LprService.cs
│   │       └── CacheService.cs
│   │
│   ├── PkLots.Application/               # Business Logic Layer
│   │   ├── UseCases/
│   │   │   ├── Auth/
│   │   │   ├── Tenants/
│   │   │   ├── ParkingSpots/
│   │   │   └── Vehicles/
│   │   ├── DTOs/
│   │   └── Mappings/
│   │
│   ├── PkLots.Api/                       # API Layer
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── SpotsController.cs
│   │   │   └── VehiclesController.cs
│   │   ├── Middleware/
│   │   │   ├── TenantMiddleware.cs
│   │   │   └── ExceptionMiddleware.cs
│   │   ├── Configuration/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── PkLots.Api.csproj
│   │
│   ├── PkLots.LprEngine/                 # Python LPR Microservice
│   │   ├── app/
│   │   │   ├── __init__.py
│   │   │   ├── main.py
│   │   │   ├── routes.py
│   │   │   └── config.py
│   │   ├── models/
│   │   │   └── plate_recognizer.py
│   │   ├── utils/
│   │   │   └── image_processor.py
│   │   ├── requirements.txt
│   │   ├── Dockerfile
│   │   └── wsgi.py
│   │
│   └── PkLots.sln
│
├── tests/
│   ├── PkLots.Api.Tests/
│   │   └── Controllers/
│   └── PkLots.Infrastructure.Tests/
│       └── Repositories/
│
├── docker/
│   ├── Dockerfile.api
│   ├── Dockerfile.lpr
│   └── nginx.conf
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── API_REFERENCE.md
│   └── DATABASE_SCHEMA.md
│
├── docker-compose.yml
├── .gitignore
├── README.md                             # ← Este arquivo
├── CONTRIBUTING.md
└── LICENSE
```

---

## 🔐 Autenticação & Segurança

### JWT Token Flow

```
┌──────────────┐
│ Flutter App  │
└──────┬───────┘
       │ POST /api/v1/auth/login
       │ {email, password}
       ▼
┌──────────────────┐
│  .NET API        │
├──────────────────┤
│ Valida senha     │
│ Gera JWT token   │
│ Retorna token    │
└──────┬───────────┘
       │ {accessToken, expiresIn}
       ▼
┌──────────────┐
│ Flutter App  │
│ Salva token  │
│ no SharedPref│
└──────┬───────┘
       │ GET /api/v1/spots
       │ Authorization: Bearer {token}
       ▼
┌──────────────────┐
│  .NET API        │
├──────────────────┤
│ Valida token     │
│ Extrai TenantId  │
│ Retorna dados    │
└──────────────────┘
```

### Multi-Tenant Isolation

Cada request tem seu `TenantId` extraído do JWT e aplicado:

```csharp
// DbContext filtra automaticamente por TenantId
modelBuilder.Entity<ParkingSpot>()
    .HasQueryFilter(p => p.TenantId == _currentTenantId);
```

### Credenciais Padrão (DEV apenas)

```
Email:    admin@pklots.local
Senha:    Senha@123
Tenant:   DefaultTenant
```

⚠️ **NUNCA usar em produção!** Mudar antes de deploy.

---

## 🗄️ Banco de Dados

### Schema Principal

```sql
-- Tenants (Pátios)
Tenants {
  id (UUID PK)
  name (string)
  email (string UNIQUE)
  total_spots (int)
  created_at (datetime)
  is_active (bool)
}

-- Users
Users {
  id (UUID PK)
  tenant_id (UUID FK)
  email (string)
  password_hash (string)
  name (string)
  role (enum: Admin, Manager, Attendant, User)
  is_active (bool)
  created_at (datetime)
}

-- Parking Spots (Vagas)
ParkingSpots {
  id (UUID PK)
  tenant_id (UUID FK)
  spot_number (string) -- A1, A2, B1, etc
  is_occupied (bool)
  is_available (bool)
  current_vehicle_id (UUID FK)
  created_at (datetime)
}

-- Vehicles (Veículos)
Vehicles {
  id (UUID PK)
  tenant_id (UUID FK)
  license_plate (string) -- ABC1234
  color (string)
  model (string)
  created_at (datetime)
}

-- Vehicle Entries (Entradas)
VehicleEntries {
  id (UUID PK)
  tenant_id (UUID FK)
  vehicle_id (UUID FK)
  parking_spot_id (UUID FK)
  entry_time (datetime)
  entry_photo_url (string)
  recognized_plate (string)
  plate_confidence (decimal) -- 0.95
  created_at (datetime)
}

-- Vehicle Exits (Saídas)
VehicleExits {
  id (UUID PK)
  tenant_id (UUID FK)
  vehicle_entry_id (UUID FK)
  exit_time (datetime)
  exit_photo_url (string)
  duration_minutes (int)
  amount (decimal)
  payment_status (enum: Pending, Paid, Cancelled)
  created_at (datetime)
}
```

### Índices Críticos

```sql
-- Performance
CREATE INDEX idx_spots_tenant_available ON parking_spots(tenant_id, is_available);
CREATE INDEX idx_entries_tenant_time ON vehicle_entries(tenant_id, entry_time DESC);
CREATE INDEX idx_vehicles_plate ON vehicles(license_plate);
```

### Cache Redis

```
Key Pattern: tenant:{tenantId}:spots:available
Value: JSON {available: 45, occupied: 5, total: 50}
TTL: 5 minutos (refresh automático)

Key Pattern: session:{userId}:{sessionId}
Value: {tenantId, role, email}
TTL: 24 horas
```

---

## 📡 API Endpoints

### Authentication
```
POST   /api/v1/auth/login           → Fazer login
POST   /api/v1/auth/register        → Registrar usuário
POST   /api/v1/auth/refresh-token   → Renovar token
POST   /api/v1/auth/logout          → Logout
```

### Parking Spots
```
GET    /api/v1/spots                → Listar vagas (com filtro)
GET    /api/v1/spots/{id}           → Detalhe de vaga
POST   /api/v1/spots                → Criar vaga
PUT    /api/v1/spots/{id}           → Atualizar vaga
DELETE /api/v1/spots/{id}           → Deletar vaga
```

### Vehicle Entry/Exit
```
POST   /api/v1/entries              → Registrar entrada (enviar foto)
GET    /api/v1/entries/{id}         → Detalhe de entrada
POST   /api/v1/exits/{entryId}      → Registrar saída (enviar foto)
GET    /api/v1/exits                → Listar saídas (com filtro de data)
```

### Tenants (Admin)
```
GET    /api/v1/tenants              → Listar pátios
POST   /api/v1/tenants              → Criar pátio
PUT    /api/v1/tenants/{id}         → Atualizar pátio
```

### Health Check
```
GET    /health                       → Status da API
GET    /health/db                    → Status do banco
GET    /health/redis                 → Status do cache
```

**Exemplo de Request:**
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@pklots.local",
    "password": "Senha@123"
  }'
```

**Exemplo de Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 86400,
  "refreshToken": "refresh_token_here"
}
```

---

## 🐍 LPR Engine (Python)

### Endpoint Principal

```
POST /v1/lpr/analyze
Content-Type: multipart/form-data

Request:
  - image: <arquivo.jpg>
  - expected_plate: "ABC1234" (opcional)

Response:
{
  "success": true,
  "plate": "ABC1234",
  "confidence": 0.95,
  "processing_time_ms": 245,
  "error": null
}
```

### Processamento

```python
1. Recebe imagem (JPEG/PNG)
2. Pré-processa (resize, normaliza)
3. Detecta região de placa (YOLOv8)
4. Extrai texto (EasyOCR)
5. Valida formato brasileiro (ABC-1234)
6. Retorna resultado com confiança
```

### Exemplo de Uso no .NET

```csharp
public class LprService : ILprService
{
    public async Task<LprResult> AnalyzePlateAsync(byte[] imageBytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(imageBytes), "image", "photo.jpg");
        
        var response = await _httpClient.PostAsync(
            "http://pklots-lpr:5000/v1/lpr/analyze",
            content);
        
        var jsonResult = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<LprResult>(jsonResult);
    }
}
```

---

## 🎯 Roadmap (4 Semanas)

### **Semana 1: Infraestrutura & Banco** 
- [x] Estrutura de pastas
- [x] Projetos .NET criados
- [x] Docker Compose pronto
- [ ] DbContext + Migrations
- [ ] Tabelas criadas (PostgreSQL)
- [ ] Testes de conectividade

**Deliverable:** Database rodando com schema completo

### **Semana 2: Autenticação & Endpoints Básicos**
- [ ] JWT Auth implementado
- [ ] Login/Register funcionando
- [ ] Middleware Multi-Tenant
- [ ] Controllers básicos (Spots, Vehicles)
- [ ] Testes unitários (Auth)

**Deliverable:** API respondendo com autenticação

### **Semana 3: Integração com Python LPR**
- [ ] Flask app estruturado
- [ ] Endpoint `/v1/lpr/analyze` implementado
- [ ] OpenCV + EasyOCR integrando
- [ ] Integração .NET ↔ Python
- [ ] Testes com imagens reais

**Deliverable:** Reconhecimento de placas funcionando

### **Semana 4: Flutter & Finalização**
- [ ] Flutter Login Screen
- [ ] Dashboard com dados de vagas
- [ ] Check-in com câmera
- [ ] Check-out e histórico
- [ ] Testes E2E completos

**Deliverable:** App multiplataforma pronto para use

---

## 🧪 Testes

### Executar Testes

```bash
# Testes unitários
cd src
dotnet test PkLots.sln

# Testes com cobertura
dotnet test PkLots.sln /p:CollectCoverage=true

# Testes específicos
dotnet test PkLots.Api.Tests --filter "AuthControllerTests"
```

### Exemplo de Teste Unitário

```csharp
[TestFixture]
public class AuthServiceTests
{
    private IAuthService _authService;
    private IUserRepository _userRepository;

    [SetUp]
    public void Setup()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _authService = new AuthService(_userRepository, _logger);
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var user = new User { Email = "test@pklots.local", PasswordHash = "hash" };
        _userRepository.GetByEmailAsync("test@pklots.local").Returns(user);

        // Act
        var token = await _authService.LoginAsync("test@pklots.local", "password");

        // Assert
        Assert.That(token, Is.Not.Null);
    }
}
```

---

## 📚 Documentação Adicional

| Documento | Descrição |
|-----------|-----------|
| [COMANDOS_INICIAIS.md](./COMANDOS_INICIAIS.md) | 10 seções passo-a-passo |
| [ROADMAP_DETALHADO.md](./ROADMAP_DETALHADO.md) | Planejamento técnico 4 semanas |
| [ARCHITECTURE.md](./docs/ARCHITECTURE.md) | Padrões de design e decisões |
| [API_REFERENCE.md](./docs/API_REFERENCE.md) | Documentação de endpoints |
| [DATABASE_SCHEMA.md](./docs/DATABASE_SCHEMA.md) | Schema e relações |
| [DEPLOYMENT.md](./docs/DEPLOYMENT.md) | Deploy em produção |

---

## 🐛 Troubleshooting

### Problema: PostgreSQL não conecta
```bash
# Verificar container
podman logs pklots-postgres

# Testar conexão manualmente
podman exec -it pklots-postgres psql -U pklots_admin -d pklots_core
```

### Problema: Porta 5432 em uso
```bash
# Liberar porta ou mudar no docker-compose.yml
sudo lsof -i :5432
kill -9 <PID>

# Ou trocar porta em docker-compose.yml
ports:
  - "5433:5432"  # localhost:5433 -> container:5432
```

### Problema: dotnet build falha
```bash
# Limpar cache
rm -rf src/bin src/obj
dotnet clean src/PkLots.sln

# Restaurar e rebuild
dotnet restore src/PkLots.sln
dotnet build src/PkLots.sln
```

### Problema: Python LPR lento
```bash
# Usar GPU se disponível (comentado no Dockerfile)
# Instalar CUDA / cuDNN para aceleração

# Ou reduzir resolução de imagem:
# image.resize((640, 480)) no image_processor.py
```

---

## 🤝 Contribuindo

1. **Fork** o repositório
2. **Crie** uma branch (`git checkout -b feature/nova-feature`)
3. **Commit** suas mudanças (`git commit -am 'Add nova-feature'`)
4. **Push** para a branch (`git push origin feature/nova-feature`)
5. **Abra** um Pull Request

### Padrões de Código

- ✅ C#: PascalCase para classes/métodos, camelCase para variáveis
- ✅ Python: snake_case para tudo (PEP 8)
- ✅ Commits: feat(module): descrição | fix: | docs: | test:
- ✅ PRs: Descrever mudanças e tester localmente

---

## 📝 Licença

Este projeto está sob a licença **MIT**. Veja [LICENSE](./LICENSE) para detalhes.

---

## 👤 Desenvolvedor

**Lucas do Nascimento Tavares**
- 🏙️ Salvador, Bahia - Brasil
- 💼 Desenvolvedor .NET Pleno (5+ anos)
- 🔗 [LinkedIn](https://linkedin.com/in/lucas-tavares)
- 📧 [Email](mailto:lucas@example.com)

---

## 📞 Suporte

### Tendo problemas?

1. Leia [COMANDOS_INICIAIS.md](./COMANDOS_INICIAIS.md) seção "Troubleshooting"
2. Verifique os logs: `podman logs <container_name>`
3. Abra uma [Issue](https://github.com/seu-usuario/pklots/issues)
4. Consulte a [Discussão](https://github.com/seu-usuario/pklots/discussions)

---

## 🎉 Agradecimentos

- Microsoft pelo framework .NET
- OpenCV e comunidade de visão computacional
- Flutter team by Google
- Comunidade Fedora/Podman

---

## 📌 Status do Projeto

- **Versão:** 0.1.0 (Alpha)
- **Última Atualização:** 2026-09-26
- **Próxima Release:** 0.2.0 (Semana 2)

```
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║  🚀 PkLots: Pronto para ser desenvolvido!                   ║
║                                                               ║
║  Estrutura ✅  |  Documentação ✅  |  Infraestrutura ✅       ║
║                                                               ║
║  Próximo passo: Execute o PROMPT_COPIAR_COLAR.md no         ║
║  Claude Code e deixe a automação criar tudo!               ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
```

---

**Made with ❤️ by Lucas Tavares | 2026**
