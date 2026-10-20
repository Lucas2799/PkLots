<p align="center">
  <img src="src/flutter/assets/imagens/logo.webp" alt="PkLots" width="260">
</p>

# PkLots: gestão de estacionamentos

![.NET](https://img.shields.io/badge/.NET-8.0-512bd4)
![Flutter](https://img.shields.io/badge/Flutter-web%20%7C%20Android-02569B)
![Python](https://img.shields.io/badge/Python-LPR-3776ab)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791)
![Podman](https://img.shields.io/badge/Podman-compose-892CA0)

O PkLots é um sistema **multiempresa (SaaS)** para administrar estacionamentos. Ele controla entrada e saída de veículos (pela placa digitada, pela foto com leitura automática ou pelo ticket com QR code), ocupação das vagas em tempo real, cobrança com caixa por operador e PIX, mensalistas, impressão de tickets e relatórios. Tudo isso para vários pátios por empresa.

O fornecedor do sistema (você) opera a plataforma e **aluga** o PkLots: cada **empresa** cliente (dono do estacionamento) assina um plano, paga uma mensalidade por pátio e tem os seus pátios, usuários, preços e a **própria** chave PIX para receber dos clientes dela.

---

## Sumário

- [O que o sistema faz](#o-que-o-sistema-faz)
- [Perfis de acesso](#perfis-de-acesso)
- [Como funciona](#como-funciona)
- [Planos e cobrança do aluguel](#planos-e-cobrança-do-aluguel)
- [Arquitetura](#arquitetura)
- [Deploy (produção / piloto)](#deploy-produção--piloto)
- [Operação do dia a dia](#operação-do-dia-a-dia)
- [Desenvolvimento](#desenvolvimento)
- [Estrutura do repositório](#estrutura-do-repositório)

---

## O que o sistema faz

| Área | Funcionalidades |
|---|---|
| **Entrada** | Pela placa digitada ou pela foto da placa (leitura automática, LPR). Aloca a vaga livre e gera o ticket com código e QR code. Entrada registrada por engano pode ser cancelada com motivo (operador até 10 min, admin a qualquer momento). |
| **Saída** | Pela placa, pela foto ou lendo o ticket (leitor USB ou câmera do celular). Calcula o valor e só libera o veículo após o pagamento. |
| **Cobrança** | Tabela de preço por pátio (por hora, 1º período + frações, diária máxima, pernoite, fim de semana) e tolerância inicial gratuita. Dinheiro (com troco), PIX (QR "copia e cola" gerado pelo próprio sistema) ou cartão (maquininha do pátio). |
| **Caixa** | Caixa por operador: abertura com troco inicial, sangria, suprimento e fechamento com conferência (esperado × contado). |
| **Link do ticket** | O QR do ticket abre uma página pública leve (`/t/...`) com tempo e valor parcial da estadia. O cliente avulso não precisa de app nem de cadastro; o link expira 24h após a saída. |
| **Mensalistas** | Mensalidade por pátio, várias placas por mensalista, registro de pagamentos (estende a validade) e vagas reservadas. Mensalista em dia entra e sai sem cobrança; vencido é cobrado como avulso. |
| **Painel** | Resumo do dia, gráfico de ocupação 24h, mapa de vagas (com manutenção), veículos estacionados e histórico por placa. |
| **Impressão** | Ticket de entrada e recibo de saída em impressora térmica de rede (ESC/POS) ou em PDF pelo navegador. |
| **Relatórios** | Para o dono: receita (avulsos + mensalidades), formas de pagamento, movimento por dia e por hora, comparativo de pátios, desempenho e diferença de caixa por operador, cancelamentos e mensalistas, com exportação em PDF. |
| **Auditoria** | Quem fez o quê, quando e em qual pátio, com filtros e o valor antes → depois nas configurações. O admin vê a própria empresa; o superadmin vê todas e também os logs técnicos da API. |
| **Administração** | Pátios, preço e tolerância, quantidade de vagas, usuários (convidados por e-mail), pagamentos (chave PIX, formas aceitas), impressora e a **assinatura** do PkLots (plano, uso, faturas). |
| **App do mensalista** | O mensalista recebe um convite por e-mail do pátio e acompanha os contratos dele: validade, veículos estacionados e histórico (só das placas do contrato, naquele pátio). Não há cadastro público. |
| **Plataforma** | O superadmin cria e ativa/desativa as empresas clientes, define planos, assinaturas e faturas, e vê o relatório de uso de cada empresa. |

## Perfis de acesso

| Perfil | Quem é | O que acessa |
|---|---|---|
| `superadmin` | Fornecedor do sistema | Tela da plataforma: empresas, planos, faturas, relatório, **auditoria de todas as empresas e logs do sistema** |
| `admin` | Dono/gerente de uma empresa | Todos os pátios da empresa, painel, caixa, mensalistas, **Administração** e **Auditoria** da empresa |
| `operador` | Funcionário do pátio | Apenas os pátios a que foi vinculado: painel, entrada/saída, caixa e mensalistas |
| `motorista` | Mensalista com convite aceito | App do mensalista (só os próprios contratos) |

Ninguém recebe senha por terceiros: admins, operadores e mensalistas são **convidados por e-mail** e criam a própria senha pelo link. "Esqueci minha senha" envia um link de redefinição.

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

### Cálculo do valor (tabela de preço)

Cada pátio tem a sua tabela, editada pelo dono em *Administração → Preço → Editar tabela de preço*, com simulação antes de salvar:

| Regra | Como funciona |
|---|---|
| **Por hora** | Proporcional ao minuto (arredondado para cima). Ex.: 4h 15m × R$ 15/h = R$ 63,75 |
| **1º período + frações** | Ex.: 1ª hora R$ 10,00 e cada 30 min ou fração R$ 3,00 (fração iniciada é cobrada inteira) |
| **Diária máxima** | Teto a cada 24h iniciadas. Ex.: diária R$ 50 → 25h custam no máximo R$ 100 |
| **Pernoite** | Janela noturna (ex.: 20h às 8h): cada noite custa no máximo o valor do pernoite; o restante segue a tabela |
| **Fim de semana** | Regras próprias para quem entra no sábado ou domingo (feriados ainda não são tratados) |
| **Tolerância** | Quem fica até N minutos não paga. Passou disso, paga o tempo todo |

A cobrança da saída mostra como o valor foi calculado (ex.: "1ª hora R$ 10,00 + 2 × 30 min de R$ 3,00"), e o ticket e a página do QR mostram a tabela do pátio. Mudanças valem para as próximas saídas.

### Pagamentos

O sistema **não intermedia dinheiro**. Cada empresa cadastra em *Administração → Pagamentos*:
- a própria chave PIX, o nome e a cidade do recebedor (o QR PIX é gerado localmente, no padrão do Banco Central);
- as formas aceitas (dinheiro, PIX e cartão na maquininha própria).

A confirmação do PIX/cartão é feita pelo operador. O provedor atual é `pix_manual`, e a estrutura permite plugar provedores com confirmação automática no futuro.

### Leitura de placas (LPR)

Microsserviço Python com [fast-alpr](https://github.com/ankandrew/fast-alpr): detector YOLOv9 + OCR ONNX, rodando **localmente na CPU**, sem API paga.
- Os modelos são baixados no build da imagem, com verificação de SHA-256.
- Tenta rotações da imagem e só aceita leituras acima de um limiar de confiança.

### E-mails

Convites, redefinição de senha e faturas vão para uma fila no banco (`EmailsPendentes`), gravada na mesma transação da regra de negócio, e são enviados em segundo plano por SMTP com novas tentativas. Falha no SMTP nunca desfaz um cadastro. Os links dos e-mails e do QR usam o endereço público `URL_PUBLICA`.

### Auditoria e logs

- **Auditoria** (admin e superadmin; ícone de checklist no painel ou na plataforma): tudo o que acontece fica registrado com
  data, pátio e **quem fez** (o usuário logado, gravado automaticamente). Entradas, recusas e cancelamentos, saídas e pagamentos,
  caixa, mensalistas, configurações (com o valor antes → depois), usuários e convites, assinatura e faturas. Filtros por período,
  pátio, tipo, situação, usuário e placa. O admin vê só a própria empresa; o superadmin vê todas ou uma escolhida.
- **Logs do sistema** (só superadmin, aba da mesma tela): o log técnico da API, com requisições, avisos e erros (com o detalhe
  da falha), para investigar um problema relatado por um cliente. Até 7 dias por consulta, guardado por 31 dias.
- O log técnico fica fora do alcance do admin de propósito: ele mistura dados de todas as empresas e detalhes internos.
- Gateway, LPR e PostgreSQL não entram na tela (o container da API não tem acesso a eles). Falhas de leitura de placa e de
  impressão já aparecem na auditoria; o resto, pelo `podman logs` (veja *Operação do dia a dia*).

### Segurança

- Senhas com BCrypt, política de senha forte e bloqueio de 15 min após 5 erros de login. O login também é limitado a 10 tentativas por minuto por IP.
- O token JWT é revalidado a cada requisição: usuário ou empresa desativados, ou perfil alterado, perdem o acesso na hora.
- Usuários com senha temporária são **obrigados a trocá-la** antes de usar o sistema.
- Em produção, a API **não sobe** sem `POSTGRES_SENHA` e `JWT_CHAVE`, nem com valores de exemplo.
- Swagger desligado e CORS restrito. Banco, Redis, API e LPR não ficam expostos para fora da máquina; só o gateway fica.
- Gateway com cabeçalhos de segurança e HTTPS automático quando há certificado.

---

## Planos e cobrança do aluguel

O superadmin cadastra os **planos** em *Plataforma → Planos* (tudo editável):

| Campo do plano | Para quê |
|---|---|
| Preço por pátio/mês | A mensalidade da empresa é `pátios ativos × preço` |
| Limites | Máximo de pátios, de vagas por pátio e de usuários (admins + operadores). Em branco = sem limite |
| Recursos | Leitura automática de placa, mensalistas, app do mensalista, relatórios e impressora térmica de rede |
| Carência | Dias após o vencimento até o bloqueio parcial (padrão: 7) |

- **Assinatura** (*Plataforma → ícone de assinatura na empresa*): plano, preço negociado por pátio (opcional), dia do vencimento (1 a 28), período de teste e observações.
- **Limites e recursos** valem na hora. O que passa do limite é recusado com mensagem clara e o app esconde os recursos fora do plano. Baixar de plano não apaga nada: só impede criar além do novo limite.
- **Empresas já existentes** foram colocadas no plano **Cortesia** (tudo liberado, sem limites, sem cobrança).

Planos que já vêm cadastrados (ponto de partida, edite à vontade). Entrada/saída, caixa, PIX, ticket em PDF, painel e auditoria estão em todos:

| Plano | Por pátio/mês | Pátios | Vagas/pátio | Usuários | Recursos | Carência |
|---|---|---|---|---|---|---|
| Essencial | R$ 89,90 | 1 | 50 | 3 | Mensalistas | 7 dias |
| Profissional | R$ 159,90 | 3 | 150 | 10 | Mensalistas, app do mensalista, relatórios, impressora de rede | 7 dias |
| Rede | R$ 239,90 | sem limite | sem limite | sem limite | Tudo, inclusive leitura automática de placa | 10 dias |
| Cortesia | R$ 0 | sem limite | sem limite | sem limite | Tudo | 7 dias |

Para um cliente piloto, use o plano certo com **período de teste** e/ou **preço negociado** na assinatura, em vez de criar um plano à parte.

**Faturas** (*Plataforma → Faturas*):
- A mensalidade é emitida sozinha **10 dias antes do vencimento** (verificação a cada hora; o botão "Emitir mensalidades agora" força a emissão). Não há fatura no período de teste nem em plano gratuito.
- Também dá para lançar **cobrança avulsa** (ex.: taxa de implantação).
- Com a **chave PIX do PkLots** cadastrada (ícone PIX na tela de faturas), cada fatura em aberto traz PIX copia e cola e QR Code. O admin da empresa vê e paga em *Administração → Assinatura*.
- A **baixa é manual**: ao receber, o superadmin clica em "Dar baixa" (forma, data e valor). Cancelamento exige motivo.

**Avisos e inadimplência:**

| Quando | O que acontece |
|---|---|
| Emissão, 5 dias antes e no vencimento | E-mail para os admins da empresa, com o PIX |
| Vencida, dentro da carência | Faixa de aviso no painel do admin. Tudo funciona |
| Carência esgotada | **Bloqueio parcial**: novas entradas e mudanças na Administração são recusadas. Saídas, caixa e mensalidades continuam liberados (nenhum carro fica preso). Admin e operadores veem o aviso; os admins recebem um e-mail |
| Pagou (baixa) | Libera na hora |
| Suspensão total | Manual: desativar a empresa na Plataforma |

A lista de empresas e o relatório da plataforma mostram a situação de cobrança, a receita recorrente e o valor em aberto.

---

## Arquitetura

```
                    ┌──────────────────────────────────────────────┐
  Navegador ───────▶│  pklots-gateway (Nginx)  :8000 / :8443        │
  App Android ─────▶│   /        → app Flutter web                  │
  QR do ticket ────▶│   /t/*     → página pública do ticket         │
                    │   /api/*   → API .NET                         │
                    └───────────────┬──────────────────────────────┘
                                    ▼
                    ┌──────────────────────────────┐      ┌──────────────────────┐
                    │ pklots-backend (.NET 8 API)  │─────▶│ pklots-lpr (Python)  │
                    │ regras, JWT, migrations,     │      │ leitura de placas    │
                    │ fila de e-mails, faturas     │      └──────────────────────┘
                    └───────┬───────────────┬──────┘─────▶ SMTP (provedor de e-mail)
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
| E-mail de teste | smtp4dev (só com `--profile dev`) | `pklots-smtp4dev` | `127.0.0.1:5080` |

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
| `URL_PUBLICA` | `http://localhost:8000` | Endereço público (domínio com HTTPS em produção). Vai no QR do ticket e nos links dos e-mails: se mudar, tickets já impressos param de abrir |
| `EMAIL_HOST` / `EMAIL_PORTA` / `EMAIL_SEGURANCA` | vazio / `25` / `auto` | SMTP para convites, senhas e faturas (Brevo, Amazon SES, Resend...). Configure SPF/DKIM no domínio. Sem host, os e-mails ficam na fila |
| `EMAIL_USUARIO` / `EMAIL_SENHA` / `EMAIL_REMETENTE` | | Credenciais e remetente do SMTP |

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
2. Em **Planos**, revise ou crie os planos. Em **Faturas → ícone PIX**, cadastre a chave PIX do PkLots.
3. Crie a **empresa**, escolhendo o plano, o primeiro pátio e o **admin** dela. Ajuste vencimento, preço negociado ou período de teste no ícone de assinatura da empresa.
4. O admin recebe um **convite por e-mail**, cria a senha e configura em **Administração** (ícone de engrenagem):
   - **Pátios**: nome, endereço, vagas e mensalidade.
   - **Preço**: tabela de preço (com a tolerância, editadas e simuladas juntas) e mensalistas.
   - **Usuários**: operadores, vinculados aos pátios (cada um recebe o convite por e-mail).
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
| Logs da API | Tela **Auditoria → Logs do sistema** (superadmin), `podman logs -f pklots-dotnet-api` ou os arquivos do volume `api_logs` (texto e `json/`) |
| Logs do gateway / LPR | `podman logs -f pklots-nginx` · `podman logs -f pklots-python-lpr` |
| Reiniciar tudo | `podman compose -f docker/podman-compose.yml restart` |
| Recuperar acesso do superadmin | `podman exec -it pklots-dotnet-api ./PkLots.Api criar-superadmin --email ...` |
| Testar a leitura de placas | Abrir `http://localhost:5000/` **no próprio servidor** (página de teste) |
| E-mails parados | Tabela `EmailsPendentes` (tentativas e último erro); verifique `EMAIL_HOST` e o log da API |
| Receber o aluguel | *Plataforma → Faturas*: filtre "Vencidas" e dê baixa ao receber |
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

### Stack completa com e-mail de teste

```bash
EMAIL_HOST=pklots-smtp4dev podman compose -f docker/podman-compose.yml --profile dev up -d --build
```

Os e-mails (convites, senhas, faturas) aparecem em `http://localhost:5080`. Para não repetir a variável, coloque `EMAIL_HOST=pklots-smtp4dev` no `docker/.env`.

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
dotnet test tests/PkLots.Application.Tests             # regras de negócio (.NET, banco em memória, ~2 s)
dotnet test tests/PkLots.Integracao.Tests              # as mesmas regras + concorrência e migrations num PostgreSQL real
cd src/flutter && flutter analyze && flutter test       # app
cd src/python && pip install -r requirements-dev.txt && pytest   # validação de placas
```

**Testes de integração (Testcontainers).** Sobem um PostgreSQL 16 descartável (a mesma imagem da produção), aplicam as migrations
num banco modelo e dão a cada teste uma cópia limpa dele; o container é removido no fim. Pegam o que o banco em memória não pega:
consultas que o PostgreSQL não traduz, chaves estrangeiras, índices únicos, `jsonb` e concorrência (duas entradas da mesma placa,
duas confirmações da mesma saída, dois carros disputando a mesma vaga). Também conferem que as migrations cobrem o modelo
(esqueceu o `dotnet ef migrations add`, o teste falha) e que a última migration desfaz e refaz.

Precisam de Docker ou Podman. Com Podman, ative o socket do usuário uma vez: `systemctl --user enable --now podman.socket`
(os testes o encontram sozinhos; em outro caminho, defina `DOCKER_HOST`). Rode-os antes de cada deploy.

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
│   ├── nginx/                   # rotas, cabeçalhos de segurança, HTTP/HTTPS, página do ticket
│   └── ssl/                     # certificado HTTPS (não versionado)
├── src/
│   ├── .net/
│   │   ├── PkLots.Core/         # entidades e constantes
│   │   ├── PkLots.Application/  # serviços e regras (tarifa, entrada, saída, caixa, mensalistas, PIX, relatórios, planos, faturas...)
│   │   ├── PkLots.Infrastructure/ # EF Core + migrations, Redis, JWT, LPR, impressora TCP, SMTP, tarefas em segundo plano
│   │   └── PkLots.Api/          # endpoints, autenticação, middlewares, comandos de servidor
│   ├── python/                  # microsserviço LPR (Flask + fast-alpr)
│   ├── web-publico/             # página pública do ticket (HTML leve, sem Flutter)
│   └── flutter/                 # app web/Android (mensalista, gestão, administração, relatórios, plataforma)
├── tests/                       # testes .NET (unidade e integração com PostgreSQL)
└── docs/                        # documentação do projeto
```
