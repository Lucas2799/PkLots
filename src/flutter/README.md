# PkLots - App Flutter

App do PkLots para **motoristas** e para **gestão** (admin/operador), em Android, iOS e Web.
A tela inicial depende do perfil do usuário logado:

| Perfil | Tela | Funcionalidades |
|---|---|---|
| motorista | Início | vagas disponíveis, meu estacionamento (duração e valor estimado), iniciar saída com recibo, cadastro de veículo, histórico de 30 dias |
| admin / operador | Painel | resumo do dia, vagas, ocupação 24h (gráfico ou tabela), entrada/saída por placa ou foto (LPR), veículos estacionados, mapa de vagas com manutenção, histórico por placa |

As telas se atualizam sozinhas a cada 15 segundos.

## Rodar

### Web, pelo compose (recomendado)

O gateway compila o app e o serve junto com a API:

```bash
podman compose -f docker/podman-compose.yml up -d --build
# http://localhost:8000
```

Usuários de teste: `admin@pklots.local` / `Admin@123`, `operator@pklots.local` / `Operator@123`,
`driver@example.com` / `Driver@123`.

### Desenvolvimento com Flutter instalado

```bash
cd src/flutter
flutter pub get
flutter run -d chrome --dart-define=API_URL=http://localhost:8000
flutter run -d android                                   # emulador usa http://10.0.2.2:8000
flutter run --dart-define=API_URL=http://192.168.0.10:8000  # aparelho físico: IP da máquina
```

### Sem Flutter instalado (via container)

```bash
podman run --rm -v $PWD/src/flutter:/app:Z -v pklots_pub_cache:/root/.pub-cache -w /app \
  ghcr.io/cirruslabs/flutter:stable sh -c "flutter analyze && flutter test"
```

## Estrutura

```
lib/
├── config/      endereço da API (API_URL)
├── tema/        cores e tema da identidade visual
├── modelos/     modelos das respostas da API
├── servicos/    cliente HTTP e sessão (SharedPreferences)
├── estado/      providers: sessão, motorista, gestão
├── telas/       login, cadastro, motorista/, gestao/
├── widgets/     logo, cartão de vagas, gráfico, recibo...
└── util/        formatadores (R$, datas, placas) e validadores
```

A identidade visual vem de `docs/preview.webp` (copiada para `assets/imagens/`);
os ícones de Android, iOS e Web foram gerados a partir do pino da logo.
