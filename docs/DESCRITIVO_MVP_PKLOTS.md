# 🅿️ PkLots - Descrição Detalhada do MVP (Versão 1.0)

**Documento:** Descritivo Técnico e Funcional  
**Versão:** 1.0 (MVP - Minimum Viable Product)  
**Data:** Setembro 2026  
**Desenvolvedor:** Lucas Tavares  
**Status:** Planejamento Inicial  

---

## 📌 1. O QUE É PKLOTS (MVP)

### Definição Executiva

PkLots MVP é uma **solução digital de gerenciamento de entrada e saída de veículos em estacionamentos**, que automatiza o registro de estacionamentos através de reconhecimento automático de placas via IA (Inteligência Artificial), eliminando a necessidade de atendentes manuais e tíquetes de papel.

### Escopo da Versão Inicial

A versão 1.0 é um **Produto Mínimo Viável (MVP)** focado em:

- ✅ **Um pátio de estacionamento** (single tenant na versão inicial)
- ✅ **Entrada e saída de veículos** automatizadas
- ✅ **Reconhecimento de placas** via câmera + IA
- ✅ **Dashboard de monitoramento** básico
- ✅ **App mobile** para motoristas verem vagas
- ✅ **Relatórios simples** de ocupação

### O Que NÃO Está no MVP v1.0

- ❌ Multi-tenant (apenas 1 pátio)
- ❌ Sistema de pagamento integrado
- ❌ Integração com barreiras de estacionamento
- ❌ Análise preditiva de ocupação
- ❌ App de gerente com todas as features
- ❌ Detecção de danos em veículos
- ❌ WhatsApp ou notificações automáticas
- ❌ Integração com SAP/sistemas legados
- ❌ Tema escuro
- ❌ Relatórios avançados

---

## 🎯 2. OBJETIVO PRINCIPAL

### Por Que Existe?

Resolver o problema de **estacionamentos tradicionais sem automação**, que sofrem com:

```
PROBLEMA                           SOLUÇÃO PkLots
───────────────────────────────────────────────────
Entrada manual (fila)              → Automática (câmera)
Sem informação de vagas            → Real-time (app)
Saída manual (demorada)            → Automática (câmera)
Perda de tíquetes                  → Digital (app)
Sem histórico de ocupação          → Relatórios automáticos
Alto custo com atendentes          → Reduzido em 60%
Sem segurança de dados             → Rastreamento completo
```

### Usuários Finais do MVP

1. **Motoristas** - Entram e saem sem tocar em nada
2. **Operador (1 pessoa)** - Monitora problemas e faz manutenção
3. **Proprietário** - Vê ocupação e receita

---

## 📋 3. FUNCIONALIDADES PRINCIPAIS (MVP)

### 3.1 ENTRADA DE VEÍCULOS

#### Fluxo Automático

```
PASSO 1: DETECÇÃO
├─ Veículo se aproxima da câmera de entrada
├─ Sensor detecta presença
└─ Câmera é acionada

PASSO 2: FOTOGRAFIA
├─ Imagem da placa é capturada (1080p)
├─ Foto é salva em storage (S3/R2)
└─ Timestamp registrado (exato)

PASSO 3: RECONHECIMENTO (IA)
├─ Python LPR Engine recebe imagem
├─ EasyOCR lê a placa
├─ Claude Vision valida (se confiança < 0.8)
├─ Formato brasileiro é validado (ABC1234)
└─ Resultado retorna: {"plate": "ABC1234", "confidence": 0.95}

PASSO 4: REGISTRO NO BANCO
├─ .NET API recebe resultado da IA
├─ Cria registro de ENTRY no PostgreSQL
├─ Dados salvos:
│   ├─ Placa: ABC1234
│   ├─ Hora de entrada: 2026-09-30 10:30:45
│   ├─ Foto URL: s3://bucket/abc1234_entry_20260930_103045.jpg
│   ├─ Confiança: 0.95
│   └─ Vaga alocada: A1 (automática)
└─ Status: ESTACIONADO

PASSO 5: LIBERAÇÃO (Cancela Abre)
├─ API envia sinal para cancela
├─ Motorista avança
└─ Cancela fecha automaticamente

TEMPO TOTAL: < 5 segundos
```

#### Dados Capturados

```json
{
  "vehicleEntryId": "550e8400-e29b-41d4-a716-446655440000",
  "licensePlate": "ABC1234",
  "entryTime": "2026-09-30T10:30:45.000Z",
  "entryPhotoUrl": "s3://pklots-bucket/plates/ABC1234_entry_20260930_103045.jpg",
  "recognitionMethod": "easyocr_with_claude_fallback",
  "confidence": 0.95,
  "parkingSpotId": "spot-A1",
  "parkingSpotNumber": "A1",
  "tenantId": "default-patio",
  "status": "ACTIVE"
}
```

---

### 3.2 SAÍDA DE VEÍCULOS

#### Fluxo Automático

```
PASSO 1: DETECÇÃO
├─ Veículo se aproxima câmera de SAÍDA
├─ Sensor detecta presença
└─ Câmera é acionada

PASSO 2: FOTOGRAFIA
├─ Segunda imagem da placa capturada
├─ Foto de saída salva (auditoria)
└─ Timestamp de saída registrado

PASSO 3: RECONHECIMENTO
├─ IA lê placa novamente
├─ Valida se é a mesma placa (comparação)
├─ Se diferente: ALERTA para operador
└─ Confiança: 0.92

PASSO 4: CÁLCULO AUTOMÁTICO
├─ Sistema busca entrada dessa placa
├─ Entrada: 10:30:45 (9/30/2026)
├─ Saída: 14:45:30 (9/30/2026)
├─ Duração: 4h 14m 45s (arredonda para 4h 15m)
├─ Taxa: R$ 15/hora
├─ Cálculo: 4.25h × R$ 15 = R$ 63,75
└─ Status: PAGO (pré-calculado, sem integração de pagamento no MVP)

PASSO 5: REGISTRO DE SAÍDA
├─ API registra EXIT no PostgreSQL
└─ Dados salvos:
    ├─ Entry ID: 550e8400-e29b-41d4-a716-446655440000
    ├─ Hora de saída: 2026-09-30 14:45:30
    ├─ Foto de saída: s3://bucket/abc1234_exit_...jpg
    ├─ Duração: 14745 segundos (4h 15m)
    ├─ Valor: 63.75 (BRL)
    ├─ Status: COMPLETED
    └─ Recibo gerado: PDF digital

PASSO 6: LIBERAÇÃO AUTOMÁTICA
├─ API envia sinal para cancela de saída
├─ Cancela abre automaticamente
├─ Motorista segue adiante

TEMPO TOTAL: < 5 segundos
```

#### Dados Capturados

```json
{
  "vehicleExitId": "660e8401-e29b-41d4-a716-446655440001",
  "vehicleEntryId": "550e8400-e29b-41d4-a716-446655440000",
  "licensePlate": "ABC1234",
  "exitTime": "2026-09-30T14:45:30.000Z",
  "exitPhotoUrl": "s3://pklots-bucket/plates/ABC1234_exit_20260930_144530.jpg",
  "recognitionMethod": "easyocr",
  "confidence": 0.92,
  "durationSeconds": 14745,
  "durationFormatted": "4h 15m",
  "durationHours": 4.25,
  "hourlyRate": 15.00,
  "totalAmount": 63.75,
  "currency": "BRL",
  "status": "COMPLETED",
  "receiptUrl": "s3://pklots-bucket/receipts/ABC1234_receipt_20260930_144530.pdf"
}
```

---

### 3.3 VAGAS DE ESTACIONAMENTO

#### Estrutura de Vagas

```
PÁTIO PADRÃO (MVP):
├─ Linhas: A, B, C (3 linhas)
├─ Vagas por linha: 10
├─ Total: 30 vagas
├─ Configuração: Simples (sem tipos)
└─ Sem prioridade/reservas

CADA VAGA:
├─ ID: spot-A1, spot-B5, etc
├─ Número: A1, B5, C10
├─ Status: AVAILABLE, OCCUPIED, MAINTENANCE
├─ Ocupada por: ABC1234 (ou null)
├─ Hora ocupação: 2026-09-30T10:30:45
└─ Observações: (campo opcional)
```

#### Controle de Vagas

```
ENTRADA:
├─ Vaga ABC1234 entra → A1 fica OCCUPIED
├─ Redis atualiza: "patio:available" = 29
├─ Dashboard atualiza em tempo real (WebSocket)
└─ Motorista vê: "29 vagas disponíveis"

SAÍDA:
├─ Vaga ABC1234 sai → A1 fica AVAILABLE
├─ Redis atualiza: "patio:available" = 30
├─ Dashboard atualiza em tempo real
└─ App motorista vê: "30 vagas disponíveis"

CACHE (Redis):
├─ Key: "patio:default:spots:status"
├─ Value: {
│    "total": 30,
│    "available": 28,
│    "occupied": 2,
│    "maintenance": 0,
│    "lastUpdate": "2026-09-30T14:45:30"
│  }
└─ TTL: 5 minutos (atualiza sozinho)
```

---

### 3.4 DASHBOARD (Gerente)

#### Tela Principal

```
┌──────────────────────────────────────────────────────┐
│ PKLOTS DASHBOARD - PÁTIO DEFAULT                    │
├──────────────────────────────────────────────────────┤
│                                                      │
│  📊 RESUMO DE HOJE                                  │
│  ──────────────────────────────────────────────────  │
│  ✅ Entradas: 156                                    │
│  ✅ Saídas: 154                                      │
│  💰 Receita: R$ 2.847,50                            │
│  ⏰ Média de estadia: 2h 34min                       │
│                                                      │
│  🅿️ STATUS ATUAL DE VAGAS                           │
│  ──────────────────────────────────────────────────  │
│  Disponíveis: 28 / 30 [████░░░░░░░░░░░░] 93%        │
│  Ocupadas: 2 / 30    [██░░░░░░░░░░░░░░░░] 7%        │
│                                                      │
│  📈 OCUPAÇÃO (últimas 24h)                          │
│  ──────────────────────────────────────────────────  │
│  00:00 ███░░░░░░░░  15%  │  12:00 ██████████  100%  │
│  04:00 ██░░░░░░░░░  10%  │  16:00 █████░░░░░  85%   │
│  08:00 █████░░░░░░  45%  │  20:00 ███░░░░░░░░  25%   │
│                                                      │
│  🚗 VEÍCULOS ESTACIONADOS AGORA                      │
│  ──────────────────────────────────────────────────  │
│  [ ABC1234 ] Entrada: 10:30  Duração: 4h 15min  A1  │
│  [ XYZ5678 ] Entrada: 12:45  Duração: 2h 0min   C7  │
│                                                      │
│  ⚠️ ALERTAS                                          │
│  ──────────────────────────────────────────────────  │
│  ✅ Sistema operacional                             │
│  ✅ Câmera entrada: OK                              │
│  ✅ Câmera saída: OK                                │
│  ✅ Redis: OK                                       │
│                                                      │
└──────────────────────────────────────────────────────┘
```

#### Funcionalidades

```
VISUALIZAR:
✅ Número de vagas disponíveis (tempo real)
✅ Ocupação atual do pátio (%)
✅ Gráfico de ocupação últimas 24h
✅ Veículos estacionados (placa, hora, duração)
✅ Receita de hoje (soma de saídas)
✅ Status dos equipamentos (câmeras, sensores)

AÇÕES:
✅ Marcar vaga como MANUTENÇÃO
✅ Ver histórico de um veículo (placa)
✅ Exportar relatório do dia (PDF)
✅ Resetar contadores manuais
❌ Não edita preços (não no MVP)
❌ Não cria usuários (não no MVP)
```

---

### 3.5 APP FLUTTER (Motorista)

#### Tela de Login

```
┌─────────────────────────────────┐
│                                 │
│    🅿️ PKLOTS                     │
│   Gestão de Estacionamentos     │
│                                 │
│  ┌──────────────────────────┐   │
│  │ Email                    │   │
│  │ usuario@example.com      │   │
│  └──────────────────────────┘   │
│                                 │
│  ┌──────────────────────────┐   │
│  │ Senha                    │   │
│  │ ••••••••                 │   │
│  └──────────────────────────┘   │
│                                 │
│  [ LOGIN ]                      │
│  [ Criar Conta ]                │
│                                 │
└─────────────────────────────────┘
```

#### Tela Principal (Dashboard)

```
┌─────────────────────────────────┐
│ 🅿️ PKLOTS               👤 Lucas │
├─────────────────────────────────┤
│                                 │
│  📍 PÁTIO: Estacionamento ABC   │
│  ⏰ Hora: 14:45 | Data: 30/09   │
│                                 │
│  ┌───────────────────────────┐  │
│  │ 📊 VAGAS DISPONÍVEIS      │  │
│  │                           │  │
│  │      28 / 30              │  │
│  │                           │  │
│  │   [████░░░░░░░░░░]        │  │
│  │        93% disponível     │  │
│  └───────────────────────────┘  │
│                                 │
│  ┌───────────────────────────┐  │
│  │ 🚗 MEU ESTACIONAMENTO     │  │
│  │                           │  │
│  │ Placa: ABC1234           │  │
│  │ Estacionado: 4h 15min    │  │
│  │ Vaga: A1                 │  │
│  │ Valor: R$ 63,75          │  │
│  │                           │  │
│  │  [ INICIAR SAÍDA ]        │  │
│  └───────────────────────────┘  │
│                                 │
│  ┌───────────────────────────┐  │
│  │ 📋 MEUS ACESSOS           │  │
│  │                           │  │
│  │ 30/set - 4h 15min -  R$63│  │
│  │ 29/set - 2h 30min -  R$37│  │
│  │ 28/set - 6h 00min -  R$90│  │
│  └───────────────────────────┘  │
│                                 │
├─────────────────────────────────┤
│ [ Histórico ]  [ Ajuda ]  [Sair]│
└─────────────────────────────────┘
```

#### Funcionalidades

```
FUNCIONAR:
✅ Mostrar vagas disponíveis (tempo real)
✅ Mostrar se tem carro estacionado
✅ Informar hora de entrada
✅ Calcular duração automática
✅ Estimar valor cobrado
✅ Botão "INICIAR SAÍDA"
✅ Histórico de acessos (últimos 30 dias)
✅ Recibo em PDF
✅ Push notification na saída

NÃO FUNCIONA NO MVP:
❌ Integração com câmera
❌ QR code para buscar carro
❌ Reserva de vagas
❌ Pagamento online
❌ Mapa do pátio
❌ Avaliações
❌ Suporte por chat
```

---

### 3.6 AUTENTICAÇÃO (Segurança Básica)

#### Login

```
TELA DE LOGIN:
├─ Email: usuario@example.com
├─ Senha: ••••••••
└─ Botão: [ LOGIN ]

BACKEND (.NET):
├─ Valida email/senha no PostgreSQL
├─ Gera JWT token (24h de validade)
├─ Retorna: {
│    "accessToken": "eyJhbGciOiJIUzI1...",
│    "expiresIn": 86400,
│    "userRole": "driver"
│  }
└─ Token salvo no app (SharedPreferences)

REQUISIÇÕES SUBSEQUENTES:
├─ Header: Authorization: Bearer {token}
├─ API valida token antes de responder
└─ Se expirado: reauthenticar
```

#### Usuários Padrão (MVP)

```
1. ADMIN (Gerente)
   ├─ Email: admin@pklots.local
   ├─ Senha: Admin@123
   ├─ Acesso: Dashboard completo
   └─ Permissão: Leitura total, ações básicas

2. DRIVER (Motorista)
   ├─ Email: driver@example.com
   ├─ Senha: Driver@123
   ├─ Acesso: Apenas seu estacionamento
   └─ Permissão: Ver vagas, iniciar saída

3. OPERATOR (Operador - Local)
   ├─ Email: operator@pklots.local
   ├─ Senha: Operator@123
   ├─ Acesso: Dashboard + alertas
   └─ Permissão: Resolver problemas, marcar manutenção
```

---

## 📱 4. ARQUITETURA TÉCNICA

### 4.1 Stack de Tecnologias

```
CAMADA                 TECNOLOGIA           VERSÃO
──────────────────────────────────────────────────
Frontend               Flutter              3.13+
├─ State Management    Provider             6.0+
├─ HTTP Client         http package         1.1+
└─ Local Storage       SharedPreferences    2.2+

Backend API            .NET                 8.0
├─ Framework           Minimal API          8.0+
├─ ORM                 Entity Framework     8.0+
├─ Authentication      JWT                  8.0+
├─ Logging             Serilog              4.0+
└─ CQRS Pattern        MediatR              12.1+

Microsserviço IA       Python               3.11
├─ Framework           Flask                3.0
├─ OCR                 EasyOCR             1.7
├─ Vision              Claude API          (optional fallback)
└─ Image Processing    OpenCV              4.8+

Banco de Dados         PostgreSQL           16
├─ Migrations          EF Core Migrations   8.0+
├─ Índices             Automáticos          (tuned)
└─ Backup              Automático diário    (script)

Cache                  Redis                7
├─ Ocupação em tempo real
├─ Sessões de usuário
└─ TTL: 5 minutos

Storage Imagens        Cloudflare R2        (S3 compatible)
├─ Fotos de entrada
├─ Fotos de saída
└─ Recibos (PDF)

Orquestração           Podman Compose       (local)
├─ PostgreSQL container
├─ Redis container
├─ API .NET container
├─ Python LPR container
└─ Nginx gateway

Reverse Proxy          Nginx                Alpine
├─ CORS
├─ Rate limiting
└─ Roteamento
```

### 4.2 Diagrama de Componentes

```
┌─────────────────────────────────────────┐
│   MOTORISTA                             │
│   (Flutter App Mobile/Web)              │
└────────────────┬────────────────────────┘
                 │ (HTTPS)
                 ▼
┌─────────────────────────────────────────┐
│   NGINX - API GATEWAY                   │
│   (Rate Limit, CORS)                    │
└────────────────┬────────────────────────┘
                 │
     ┌───────────┼───────────┐
     │           │           │
     ▼           ▼           ▼
┌─────────┐  ┌──────────┐  ┌─────────────┐
│ .NET    │  │ Python   │  │  PostgreSQL │
│ API     │  │ LPR      │  │  Database   │
│ (8080)  │  │ (5000)   │  │  (5432)     │
└────┬────┘  └────┬─────┘  └──────┬──────┘
     │            │               │
     └────────────┼───────────────┘
                  │
                  ▼
         ┌─────────────────┐
         │  Redis (6379)   │
         │  Cache          │
         └─────────────────┘
                  │
                  ▼
         ┌─────────────────┐
         │ Cloudflare R2   │
         │ (Image Storage) │
         └─────────────────┘
```

---

## 🗄️ 5. BANCO DE DADOS (Schema)

### 5.1 Tabelas Principais

#### TENANTS (Pátios)
```sql
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    max_spots INT DEFAULT 30,
    hourly_rate DECIMAL(10, 2) DEFAULT 15.00,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índice para buscar por nome
CREATE INDEX idx_tenants_name ON tenants(name);
```

#### USERS (Usuários)
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(500) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    role VARCHAR(20) DEFAULT 'driver', -- admin, driver, operator
    is_active BOOLEAN DEFAULT true,
    last_login TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_tenant_role ON users(tenant_id, role);
```

#### PARKING_SPOTS (Vagas)
```sql
CREATE TABLE parking_spots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    spot_number VARCHAR(10) NOT NULL, -- A1, B5, C10
    line_letter CHAR(1) NOT NULL, -- A, B, C
    spot_index INT NOT NULL, -- 1, 2, 3... 10
    is_available BOOLEAN DEFAULT true,
    is_occupied BOOLEAN DEFAULT false,
    current_vehicle_id UUID,
    status VARCHAR(20) DEFAULT 'available', -- available, occupied, maintenance
    occupied_since TIMESTAMP,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_spots_tenant_available ON parking_spots(tenant_id, is_available);
CREATE INDEX idx_spots_status ON parking_spots(tenant_id, status);
```

#### VEHICLES (Veículos)
```sql
CREATE TABLE vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    license_plate VARCHAR(10) UNIQUE NOT NULL, -- ABC1234
    color VARCHAR(50),
    model VARCHAR(100),
    owner_name VARCHAR(255),
    phone VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_vehicles_plate ON vehicles(license_plate);
CREATE INDEX idx_vehicles_tenant ON vehicles(tenant_id);
```

#### VEHICLE_ENTRIES (Entradas)
```sql
CREATE TABLE vehicle_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    vehicle_id UUID REFERENCES vehicles(id),
    license_plate VARCHAR(10) NOT NULL,
    parking_spot_id UUID REFERENCES parking_spots(id),
    entry_time TIMESTAMP NOT NULL,
    entry_photo_url VARCHAR(500),
    recognition_method VARCHAR(50), -- easyocr, claude_vision
    confidence DECIMAL(3, 2), -- 0.95
    status VARCHAR(20) DEFAULT 'active', -- active, completed, error
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_entries_tenant_time ON vehicle_entries(tenant_id, entry_time DESC);
CREATE INDEX idx_entries_plate ON vehicle_entries(license_plate);
CREATE INDEX idx_entries_status ON vehicle_entries(status);
```

#### VEHICLE_EXITS (Saídas)
```sql
CREATE TABLE vehicle_exits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    vehicle_entry_id UUID NOT NULL REFERENCES vehicle_entries(id),
    license_plate VARCHAR(10) NOT NULL,
    exit_time TIMESTAMP NOT NULL,
    exit_photo_url VARCHAR(500),
    duration_seconds INT,
    duration_formatted VARCHAR(20), -- 4h 15m
    hourly_rate DECIMAL(10, 2),
    total_amount DECIMAL(10, 2),
    currency VARCHAR(3) DEFAULT 'BRL',
    payment_status VARCHAR(20) DEFAULT 'calculated', -- calculated, paid, error
    receipt_url VARCHAR(500),
    status VARCHAR(20) DEFAULT 'completed',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_exits_tenant_time ON vehicle_exits(tenant_id, exit_time DESC);
CREATE INDEX idx_exits_entry_id ON vehicle_exits(vehicle_entry_id);
CREATE INDEX idx_exits_payment ON vehicle_exits(payment_status);
```

#### AUDIT_LOG (Auditoria)
```sql
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    action VARCHAR(100), -- entry_success, exit_success, lpr_failed, etc
    status VARCHAR(20), -- success, error, warning
    license_plate VARCHAR(10),
    details JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_tenant ON audit_logs(tenant_id, created_at DESC);
CREATE INDEX idx_audit_action ON audit_logs(action);
```

---

## 📊 6. FLUXO DE DADOS COMPLETO

### Cenário: Um dia no PkLots MVP

```
MANHÃ (06:00 - 12:00)
├─ 06:00 - Sistema inicializa
│  ├─ PostgreSQL: ✅ ONLINE
│  ├─ Redis: ✅ ONLINE
│  ├─ Python LPR: ✅ ONLINE
│  ├─ .NET API: ✅ ONLINE
│  └─ Dashboard carrega: "30 vagas disponíveis"
│
├─ 06:30 - Primeiro carro entra
│  ├─ Câmera fotografa: ABC1234
│  ├─ IA lê placa: ABC1234 (confiance: 0.97)
│  ├─ Banco registra entrada: 06:30:15
│  ├─ Vaga A1 atualiza para OCCUPIED
│  ├─ Redis: "vagas_disponíveis" = 29
│  ├─ Dashboard atualiza em tempo real
│  ├─ App motorista: "Entrada registrada"
│  └─ Cancela abre automaticamente
│
├─ 07:00 - Mais 20 carros entram
│  ├─ Mesmo fluxo para cada um
│  ├─ Vagas: 30 → 10
│  ├─ Dashboard mostra ocupação crescendo
│  └─ Todas as entradas registradas
│
└─ 12:00 - PICO DO MEIO-DIA
   ├─ Ocupação: 87% (26/30 vagas)
   ├─ 5 carros saem
   ├─ Saídas registram duração + valor
   ├─ Recibos gerados automaticamente
   ├─ Receita do dia até agora: R$ 1.245
   └─ Vagas voltam para 9 livres

TARDE (12:00 - 18:00)
├─ 14:00 - Ocupação máxima atingida
│  ├─ 28/30 vagas ocupadas (93%)
│  ├─ Dashboard mostra: ⚠️ QUASE CHEIO
│  ├─ 3 carros saem
│  ├─ Imediatamente 3 entram
│  └─ Ocupação estável
│
├─ 16:00 - Começa a esvaziar
│  ├─ 10 saídas registradas
│  ├─ Ocupação: 65%
│  ├─ Receita: R$ 2.100
│  └─ Mais vagas ficando disponíveis
│
└─ 18:00 - FIM DA TARDE
   ├─ Ocupação: 40% (12/30)
   ├─ 50+ transações completadas
   ├─ Receita do dia: R$ 2.847,50
   └─ Dashboard mostra gráfico de ocupação

NOITE (18:00 - 06:00)
├─ 20:00 - Fim de expediente
│  ├─ Ocupação: 25%
│  └─ Sistema continua operando
│
└─ 06:00 (PRÓXIMO DIA) - Relatório automático
   ├─ Resumo do dia anterior:
   │  ├─ Entradas: 156
   │  ├─ Saídas: 154
   │  ├─ Receita: R$ 2.847,50
   │  ├─ Ocupação média: 65%
   │  ├─ Pico: 93%
   │  └─ Falhas de IA: 2 (1.3%)
   │
   └─ Backup automático do banco de dados
```

---

## ⚠️ 7. LIMITAÇÕES DO MVP

### O que funciona parcialmente ou não funciona

```
❌ PAGAMENTO INTEGRADO
   └─ MVP apenas calcula o valor
   └─ Futuro: Integração com PIX, cartão

❌ MÚLTIPLOS PÁTIOS (Multi-tenant)
   └─ MVP: Apenas 1 pátio por instalação
   └─ Futuro: Versão 2.0 com múltiplos pátios

❌ INTEGRAÇÃO COM BARREIRAS
   └─ MVP: Envia sinal para abrir (simulado)
   └─ Futuro: Integração real com equipamento

❌ MANUTENÇÃO DE VAGAS
   └─ MVP: Operador marca manualmente
   └─ Futuro: Automático com sensores

❌ SISTEMA DE PRIORIDADE
   └─ MVP: Todas as vagas iguais
   └─ Futuro: PCD, grávidas, executivos

❌ RESERVAS
   └─ MVP: Não existe
   └─ Futuro: Reservar vaga por tempo

❌ NOTIFICAÇÕES
   └─ MVP: Apenas na tela
   └─ Futuro: Push notification, SMS, WhatsApp

❌ RELATÓRIOS AVANÇADOS
   └─ MVP: Apenas dia atual
   └─ Futuro: Período customizado, análises

❌ TEMA ESCURO
   └─ Futuro: Implementar tema dark mode

❌ INTEGRAÇÃO COM SAP/ERP
   └─ MVP: Independente
   └─ Futuro: APIs para sistemas legados
```

---

## 📈 8. MÉTRICAS DO MVP

### Objetivos de Performance

```
LATÊNCIA
├─ Reconhecimento de placa: < 1s (média)
├─ Banco de dados: < 100ms
├─ API response: < 200ms
└─ Atualização dashboard: < 500ms (WebSocket)

CONFIABILIDADE
├─ Uptime: 99.5% (máximo 3h paradas/mês)
├─ Taxa de sucesso IA: > 95% (placa lida)
├─ Backup automático: Diário
└─ Recovery time: < 30 minutos

ESCALABILIDADE
├─ Suporta: até 100 carros/hora
├─ Vagas: até 30 (expansível manualmente)
├─ Usuários simultâneos: 10
├─ Requisições/segundo: 20

SEGURANÇA
├─ Senhas: Hashed (bcrypt)
├─ JWT: 24h de validade
├─ HTTPS: Obrigatório em produção
├─ Dados: Criptografados em repouso (futuro)
└─ Logs: Auditoria completa
```

---

## 🎯 9. REQUISITOS MÍNIMOS DE HARDWARE

### Para Desenvolvimento (Local)

```
CPU:      2+ cores (Intel i5 ou equivalente)
RAM:      4GB mínimo (8GB recomendado)
Disco:    10GB livres
GPU:      Não necessária (Python LPR roda em CPU)
Rede:     Internet estável para AWS/R2
```

### Para Produção (Piloto)

```
CPU:      4+ cores (Intel Xeon ou equivalente)
RAM:      8GB+ (para PostgreSQL + Redis)
Disco:    50GB (fotos, logs, backup)
GPU:      Opcional (acelera IA)
Rede:     1Gbps+ com backup
Backup:   Storage externo ou cloud
Câmeras:  2 (entrada + saída) 1080p
```

---

## 📝 10. DOCUMENTAÇÃO INCLUÍDA

```
README_EXPLICADO.md     - Conceito geral do projeto
DESCRITIVO_MVP.md       - Este documento
BRAND_GUIDELINES.md     - Identidade visual
COMANDOS_INICIAIS.md    - Setup passo-a-passo
ROADMAP_DETALHADO.md    - 4 semanas de desenvolvimento
PROMPT_COPIAR_COLAR.md  - Automação Claude Code
API_REFERENCE.md        - Endpoints (a criar)
DATABASE_SCHEMA.md      - SQL completo (a criar)
DEPLOYMENT.md           - Deploy em produção (a criar)
```

---

## ✅ 11. CHECKLIST DE ENTREGA DO MVP

```
BACKEND (.NET)
- [ ] DbContext com todas as entidades
- [ ] Migrations PostgreSQL aplicadas
- [ ] Autenticação JWT funcionando
- [ ] Endpoints de entrada registrados
- [ ] Endpoints de saída registrados
- [ ] Dashboard API funcionando
- [ ] Cache Redis integrado
- [ ] Logging estruturado
- [ ] Tratamento de erros
- [ ] Testes unitários básicos

BACKEND (Python LPR)
- [ ] Flask app estruturado
- [ ] Endpoint /v1/lpr/analyze funcionando
- [ ] EasyOCR integrando
- [ ] Claude Vision como fallback
- [ ] Validação de placa brasileira
- [ ] Salvar fotos em R2
- [ ] Logging de erros

FRONTEND (Flutter)
- [ ] Login/autenticação
- [ ] Tela de status de vagas
- [ ] Mostrar estacionamento ativo
- [ ] Iniciar saída
- [ ] Histórico de acessos
- [ ] Recibo em PDF
- [ ] Responsivo (mobile + web)

INFRA
- [ ] Docker Compose funcionando
- [ ] PostgreSQL online
- [ ] Redis online
- [ ] Nginx configurado
- [ ] Backup automático
- [ ] Logs centralizados

DOCUMENTAÇÃO
- [ ] README completo
- [ ] API reference
- [ ] Database schema
- [ ] Guia de instalação
- [ ] Troubleshooting

TESTES
- [ ] 20+ testes unitários
- [ ] Teste E2E: entrada+saída
- [ ] Teste IA: reconhecimento
- [ ] Teste de performance
- [ ] Teste de segurança
```

---

## 🎉 RESUMO EXECUTIVO

### PkLots MVP é:

✅ Uma **solução completa de automação de estacionamentos**  
✅ Que **reconhece placas automaticamente com IA**  
✅ Registra **entrada e saída sem intervenção humana**  
✅ Mostra **vagas em tempo real** para motoristas  
✅ Gera **recibos digitais automáticos**  
✅ Oferece **relatórios de ocupação e receita**  
✅ Funciona **offline e online**  
✅ Construída com **stack moderno e escalável**  
✅ Pronta para **expansão futura (múltiplos pátios)**  

### Impacto:

💰 Reduz custos operacionais em **60%**  
⏱️ Reduz tempo de entrada/saída de **3 min → 5 seg**  
📊 Oferece dados para **decisões baseadas em dados**  
🔒 Aumenta **segurança e rastreamento**  
😊 Melhora **experiência do motorista**  

---

```
╔════════════════════════════════════════════════════╗
║                                                    ║
║  🚀 PkLots MVP v1.0 - Pronto para Começar!       ║
║                                                    ║
║  Próximo Passo: Execute Claude Code com o        ║
║  PROMPT_COPIAR_COLAR.md para criar estrutura     ║
║                                                    ║
║  Tempo estimado: 4-5 semanas (1 desenvolvedor)   ║
║                                                    ║
╚════════════════════════════════════════════════════╝
```

**Versão:** 1.0 MVP  
**Data:** Setembro 2026  
**Desenvolvedor:** Lucas Tavares  
**Status:** Pronto para desenvolvimento
