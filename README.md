<p align="center">
  <img src="src/flutter/assets/imagens/logo.webp" alt="PkLots" width="260">
</p>

# PkLots: gestão de estacionamentos

![.NET](https://img.shields.io/badge/.NET-8.0-512bd4)
![Flutter](https://img.shields.io/badge/Flutter-web%20%7C%20Android-02569B)
![Python](https://img.shields.io/badge/Python-LPR-3776ab)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791)
![Podman](https://img.shields.io/badge/Podman-compose-892CA0)

O PkLots é um sistema **multiempresa (SaaS)** para administrar estacionamentos. Ele controla entrada e saída de veículos (pela placa digitada, pela foto com leitura automática ou pelo ticket com QR code), ocupação das vagas em tempo real, cobrança com caixa por operador e PIX, mensalistas e impressão de tickets. Tudo isso para vários pátios por empresa.

O fornecedor do sistema (você) opera a plataforma. Cada **empresa** cliente (dono do estacionamento) tem os seus pátios, usuários, preços e a **própria** chave PIX/contrato de pagamento.

---

## Sumário

- [O que o sistema faz](#o-que-o-sistema-faz)
- [Perfis de acesso](#perfis-de-acesso)
- [Como funciona](#como-funciona)
- [Arquitetura](#arquitetura)
- [Deploy (produção / piloto)](#deploy-produção--piloto)
- [Operação do dia a dia](#operação-do-dia-a-dia)
- [Desenvolvimento](#desenvolvimento)
- [Estrutura do repositório](#estrutura-do-repositório)

---

## O que o sistema faz

| Área | Funcionalidades |
|---|---|
| **Entrada** | Pela placa digitada ou pela foto da placa (leitura automática, LPR). Aloca a vaga livre e gera o ticket com código e QR code. |
| **Saída** | Pela placa, pela foto ou lendo o ticket (leitor USB ou câmera do celular). Calcula o valor e só libera o veículo após o pagamento. |
| **Cobrança** | Valor por hora proporcional ao minuto e tolerância inicial gratuita. Dinheiro (com troco), PIX (QR "copia e cola" gerado pelo próprio sistema) ou cartão (maquininha do pátio). |
| **Caixa** | Caixa por operador: abertura com troco inicial, sangria, suprimento e fechamento com conferência (esperado × contado). |
| **Mensalistas** | Mensalidade por pátio, várias placas por mensalista, registro de pagamentos (estende a validade) e vagas reservadas. Mensalista em dia entra e sai sem cobrança; vencido é cobrado como avulso. |
| **Painel** | Resumo do dia, gráfico de ocupação 24h, mapa de vagas (com manutenção), veículos estacionados e histórico por placa. |
| **Impressão** | Ticket de entrada e recibo de saída em impressora térmica de rede (ESC/POS) ou em PDF pelo navegador. |
| **Administração** | Pátios, preço e tolerância, quantidade de vagas, usuários e perfis, pagamentos (chave PIX, formas aceitas) e impressora. |
| **Motorista** | O motorista vê as vagas disponíveis, o próprio estacionamento (tempo e valor estimado), os veículos e o histórico. |
| **Plataforma** | O superadmin cria e ativa/desativa as empresas clientes e o primeiro admin de cada uma. |

## Perfis de acesso

| Perfil | Quem é | O que acessa |
|---|---|---|
| `superadmin` | Fornecedor do sistema | Tela da plataforma: empresas clientes |
| `admin` | Dono/gerente de uma empresa | Todos os pátios da empresa, painel, caixa, mensalistas e **Administração** |
| `operador` | Funcionário do pátio | Apenas os pátios a que foi vinculado: painel, entrada/saída, caixa e mensalistas |
| `motorista` | Cliente final | App do motorista |

Os dados de cada empresa são isolados no banco (filtro por empresa em todas as consultas). O pátio em uso vai em cada requisição (cabeçalho `X-Patio-Id`) e é validado contra as permissões do usuário.

---

## Como funciona

### Fluxo de entrada

```
Operador digita a placa / tira foto / ─▶ API valida a placa (Mercosul ou antiga)
                                          │  foto? ─▶ serviço LPR lê a placa (local, sem custo por leitura)
                                          ▼
                       É mensalista? ─▶ em dia: entra sem cobrança · vencido: será cobrado como avulso
                                          ▼
                       Aloca uma vaga livre (respeitando as reservadas a mensalistas)
                                          ▼
                       Gera o ticket (código curto + QR) ─▶ impressora de rede ou PDF no navegador
```

### Fluxo de saída (em duas etapas)

1. **Cobrança**: o operador informa a placa, a foto ou o ticket. O sistema calcula o valor e o **congela por 15 minutos**. Se a forma for PIX, mostra o QR "copia e cola" com a chave PIX da empresa.
2. **Confirmação**: o operador registra o pagamento (dinheiro com troco, PIX ou cartão). Isso **exige o caixa aberto**. A vaga é liberada e o recibo pode ser impresso.

Saídas com valor zero (dentro da tolerância ou mensalista em dia) são liberadas sem pagamento. **Não existe saída de veículo pagante sem pagamento registrado.**

### Cálculo do valor

- A duração é arredondada para cima ao minuto e cobrada proporcionalmente ao valor/hora do pátio. Exemplo: 4h 15m × R$ 15/h = R$ 63,75.
- **Tolerância**: quem fica até N minutos não paga. Passou disso, paga o tempo todo.

### Pagamentos

O sistema **não intermedia dinheiro**. Cada empresa cadastra em *Administração → Pagamentos*:
- a própria chave PIX, o nome e a cidade do recebedor (o QR PIX é gerado localmente, no padrão do Banco Central);
- as formas aceitas (dinheiro, PIX e cartão na maquininha própria).

A confirmação do PIX/cartão é feita pelo operador. O provedor atual é `pix_manual`, e a estrutura permite plugar provedores com confirmação automática no futuro.

### Leitura de placas (LPR)

Microsserviço Python com [fast-alpr](https://github.com/ankandrew/fast-alpr): detector YOLOv9 + OCR ONNX, rodando **localmente na CPU**, sem API paga.
- Os modelos são baixados no build da imagem, com verificação de SHA-256.
- Tenta rotações da imagem e só aceita leituras acima de um limiar de confiança.

### Segurança

- Senhas com BCrypt, política de senha forte e bloqueio de 15 min após 5 erros de login. O login também é limitado a 10 tentativas por minuto por IP.
- O token JWT é revalidado a cada requisição: usuário ou empresa desativados, ou perfil alterado, perdem o acesso na hora.
- Usuários com senha temporária são **obrigados a trocá-la** antes de usar o sistema.
- Em produção, a API **não sobe** sem `POSTGRES_SENHA` e `JWT_CHAVE`, nem com valores de exemplo.
- Swagger desligado e CORS restrito. Banco, Redis, API e LPR não ficam expostos para fora da máquina; só o gateway fica.
- Gateway com cabeçalhos de segurança e HTTPS automático quando há certificado.

---

## Arquitetura

```
                    ┌──────────────────────────────────────────────┐
  Navegador ───────▶│  pklots-gateway (Nginx)  :8000 / :8443        │
  App Android ─────▶│   /        → app Flutter web                  │
                    │   /api/*   → API .NET                         │
                    └───────────────┬──────────────────────────────┘
                                    ▼
                    ┌──────────────────────────────┐      ┌──────────────────────┐
                    │ pklots-backend (.NET 8 API)  │─────▶│ pklots-lpr (Python)  │
                    │ regras, JWT, migrations      │      │ leitura de placas    │
                    └───────┬───────────────┬──────┘      └──────────────────────┘
                            ▼               ▼
                 ┌────────────────┐  ┌───────────────┐
                 │ PostgreSQL 16  │  │ Redis 7       │
                 │ dados          │  │ cache de vagas│
                 └────────────────┘  └───────────────┘
```

| Componente | Tecnologia | Container | Porta (host) |
|---|---|---|---|
| Gateway + app web | Nginx + Flutter web | `pklots-nginx` | `8000` (HTTP), `8443` (HTTPS), públicas |
| API | .NET 8 Minimal API, EF Core, Serilog | `pklots-dotnet-api` | `127.0.0.1:8080` |
| LPR | Python, Flask + gunicorn, fast-alpr | `pklots-python-lpr` | `127.0.0.1:5000` |
| Banco | PostgreSQL 16 | `pklots-postgres` | `127.0.0.1:5432` |
| Cache | Redis 7 | `pklots-redis` | (só rede interna) |

A API segue camadas: `Core` (entidades), `Application` (regras/serviços), `Infrastructure` (EF, Redis, JWT, LPR, impressora) e `Api` (endpoints).

---

## Deploy (produção / piloto)

### 1. Requisitos do servidor

- Linux com **Podman** e `podman compose` (ou `podman-compose`). Docker + `docker compose` também funciona.
- 2 vCPU e 4 GB de RAM recomendados (a leitura de placas usa CPU).
- Portas liberadas no firewall: `8000` e `8443`, ou as que você definir.
- Um domínio apontando para o servidor, se for usar HTTPS com certificado válido.

### 2. Baixar o projeto e configurar os segredos

```bash
git clone <url-do-repositorio> pklots && cd pklots
cp docker/.env.example docker/.env
chmod 600 docker/.env
```

Edite `docker/.env` e preencha **obrigatoriamente**:

```bash
POSTGRES_SENHA=$(openssl rand -base64 24)   # cole o valor gerado
JWT_CHAVE=$(openssl rand -base64 48)        # cole o valor gerado
```

Outras opções do `.env`:

| Variável | Padrão | Para quê |
|---|---|---|
| `GATEWAY_PORTA` / `GATEWAY_PORTA_HTTPS` | `8000` / `8443` | Portas públicas do sistema |
| `SWAGGER_HABILITADO` | `false` | Documentação da API em `/swagger` (deixe `false` em produção) |
| `DADOS_DEMONSTRACAO` | `false` | Cria empresa/pátio/usuários de teste com senhas públicas. **Nunca em produção.** |
| `AMBIENTE` | `Production` | Ambiente da API |

> O `docker/.env` não vai para o git. Guarde uma cópia segura: sem a `POSTGRES_SENHA` você não acessa o banco.

### 3. HTTPS (recomendado)

Coloque o certificado do domínio em `docker/ssl/`:

```
docker/ssl/cert.pem   # certificado + cadeia
docker/ssl/key.pem    # chave privada
```

Com os dois arquivos, o gateway sobe em HTTPS e redireciona o HTTP; sem eles, fica só em HTTP. Use Let's Encrypt (certbot) ou o certificado do seu provedor/CDN. Detalhes e um certificado autoassinado para testes estão em [`docker/ssl/LEIA-ME.md`](docker/ssl/LEIA-ME.md).

### 4. Subir

```bash
podman compose -f docker/podman-compose.yml up -d --build
podman ps        # todos devem ficar "healthy"
```

Na primeira subida a API cria o banco e aplica as migrations automaticamente. O build da primeira vez demora alguns minutos (compila o app Flutter e baixa os modelos de leitura de placas).

### 5. Criar o superadmin (você)

Não existe senha padrão. O superadmin é criado pelo servidor:

```bash
podman exec -it pklots-dotnet-api ./PkLots.Api criar-superadmin --email voce@empresa.com
```

O comando mostra uma **senha temporária**. No primeiro login o sistema exige a troca. Rodar o mesmo comando de novo **redefine** a senha, o que serve para recuperar o acesso.

### 6. Cadastrar o primeiro cliente

1. Entre em `https://seu-dominio:8443` (ou `http://IP:8000`) com o superadmin.
2. Crie a **empresa** e o **admin** dela.
3. O admin entra, troca a senha e configura em **Administração** (ícone de engrenagem):
   - **Pátios**: nome, endereço, vagas e mensalidade.
   - **Preço**: valor/hora e tolerância.
   - **Usuários**: operadores, vinculados aos pátios.
   - **Pagamentos**: chave PIX e formas aceitas.
   - **Impressão**: impressora de rede (IP/porta, largura do papel) ou PDF pelo navegador.

### 7. App Android

Gere o APK apontando para o endereço público do servidor:

```bash
cd src/flutter
flutter build apk --release --dart-define=API_URL=https://seu-dominio:8443
# APK em build/app/outputs/flutter-apk/app-release.apk
```

O APK de release só aceita **HTTPS**. Para testar na rede local em HTTP, use o modo debug (ver [Desenvolvimento](#desenvolvimento)).

### Atualizar para uma nova versão

```bash
# 1) Backup antes (as migrations rodam sozinhas ao subir a nova API)
podman exec pklots-postgres pg_dump -U pklots_admin -Fc pklots_core > backup-$(date +%F).dump

# 2) Baixar e reconstruir
git pull
podman compose -f docker/podman-compose.yml down        # NÃO use -v: apagaria os dados
podman compose -f docker/podman-compose.yml up -d --build
```

Quem estiver com o app web aberto precisa recarregar a página (F5) para pegar a versão nova.

### Backup e restauração

```bash
# Backup (agende diariamente no cron/systemd timer e copie para fora do servidor)
podman exec pklots-postgres pg_dump -U pklots_admin -Fc pklots_core > pklots-$(date +%F).dump

# Restauração
podman exec -i pklots-postgres pg_restore -U pklots_admin -d pklots_core --clean --if-exists < pklots-AAAA-MM-DD.dump
```

Os dados ficam nos volumes `postgres_data`, `redis_data` (cache) e `api_logs`.

---

## Operação do dia a dia

| Tarefa | Comando |
|---|---|
| Ver situação dos serviços | `podman ps` |
| Logs da API | `podman logs -f pklots-dotnet-api` (também em arquivo no volume `api_logs`) |
| Logs do gateway / LPR | `podman logs -f pklots-nginx` · `podman logs -f pklots-python-lpr` |
| Reiniciar tudo | `podman compose -f docker/podman-compose.yml restart` |
| Recuperar acesso do superadmin | `podman exec -it pklots-dotnet-api ./PkLots.Api criar-superadmin --email ...` |
| Testar a leitura de placas | Abrir `http://localhost:5000/` **no próprio servidor** (página de teste) |
| Acessar o banco (DBeaver etc.) | `localhost:5432`, banco `pklots_core`, usuário `pklots_admin`, senha do `docker/.env`. Só a partir do servidor ou por túnel SSH: `ssh -L 5432:localhost:5432 usuario@servidor` |

**No pátio**, o operador:
1. Abre o **caixa** com o troco inicial.
2. Registra entradas e saídas.
3. Faz sangria/suprimento quando preciso.
4. **Fecha o caixa** informando o dinheiro contado. O sistema mostra a diferença em relação ao esperado.

No celular, as ações ficam na barra inferior (Painel, Caixa, Mensalistas e Mais). No computador, ficam no topo.

---

## Desenvolvimento

### Requisitos

- .NET SDK 8, Flutter (stable), Python 3.11+ e Podman.
- Para Android: JDK 17+ (com `javac`) e Android SDK.

### Subir só a infraestrutura e rodar a API localmente

```bash
podman compose -f docker/podman-compose.yml up -d pklots-db pklots-cache pklots-lpr
cd src/.net
dotnet run --project PkLots.Api      # usa appsettings.Development.json (chave JWT de dev + dados de demonstração)
```

Em `Development`, o Swagger fica em `http://localhost:<porta>/swagger`.

### App Flutter

```bash
cd src/flutter
flutter pub get
flutter run -d chrome --dart-define=API_URL=http://localhost:8000

# Celular Android na mesma rede (depuração USB ativada): use o IP da máquina
flutter devices
flutter run -d <id-do-aparelho> --dart-define=API_URL=http://192.168.x.x:8000
```

O endereço da API é definido no build por `--dart-define=API_URL=...`. HTTP sem TLS só é permitido no build de **debug**.

### Migrations (EF Core)

```bash
cd src/.net
dotnet ef migrations add NomeDaMigration --project PkLots.Infrastructure --startup-project PkLots.Api
```

Revise a migration gerada antes de aplicar (renomeações e colunas novas com dados existentes). Ela é aplicada automaticamente quando a API sobe.

### Testes

```bash
dotnet test tests/PkLots.Application.Tests             # regras de negócio (.NET)
cd src/flutter && flutter analyze && flutter test       # app
cd src/python && pip install -r requirements-dev.txt && pytest   # validação de placas
```

---

## Estrutura do repositório

```
PkLots/
├── docker/
│   ├── podman-compose.yml       # todos os serviços
│   ├── .env.example             # modelo de configuração (copie para .env)
│   ├── Dockerfile.api           # API .NET enxuta (self-contained, alpine)
│   ├── Dockerfile.lpr           # serviço de leitura de placas
│   ├── Dockerfile.gateway       # compila o Flutter web e serve com Nginx
│   ├── nginx/                   # rotas, cabeçalhos de segurança, HTTP/HTTPS
│   └── ssl/                     # certificado HTTPS (não versionado)
├── src/
│   ├── .net/
│   │   ├── PkLots.Core/         # entidades e constantes
│   │   ├── PkLots.Application/  # serviços e regras (tarifa, entrada, saída, caixa, mensalistas, PIX...)
│   │   ├── PkLots.Infrastructure/ # EF Core + migrations, Redis, JWT, LPR, impressora TCP
│   │   └── PkLots.Api/          # endpoints, autenticação, middlewares, comandos de servidor
│   ├── python/                  # microsserviço LPR (Flask + fast-alpr)
│   └── flutter/                 # app web/Android (motorista, gestão, administração, plataforma)
├── tests/                       # testes .NET
└── docs/                        # documentação do projeto
```
