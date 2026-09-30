import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';
import 'package:pklots/app.dart';
import 'package:pklots/estado/gestao_provider.dart';
import 'package:pklots/estado/motorista_provider.dart';
import 'package:pklots/estado/sessao_provider.dart';
import 'package:pklots/servicos/armazenamento_sessao.dart';
import 'package:pklots/servicos/cliente_api.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'apoio.dart';

/// Servidor simulado com as rotas usadas pelas telas
Future<http.Response> servidor(http.Request req, {required String perfil}) async {
  switch (req.url.path) {
    case '/api/auth/login':
      final senhaCorreta = req.body.contains('Senha@123');
      return senhaCorreta
          ? json({'tokenAcesso': tokenFalso(nome: 'Lucas Tavares', perfil: perfil), 'expiraEm': 86400, 'perfil': perfil})
          : json({'title': 'Não autorizado', 'detail': 'E-mail ou senha inválidos.'}, 401);
    case '/api/vagas/resumo':
      return json(resumoVagasJson);
    case '/api/motorista/estacionamento-atual':
      return json({
        'entradaId': '1',
        'placa': 'ABC1234',
        'dataHoraEntrada': '2026-09-30T10:30:45Z',
        'duracao': '4h 15m',
        'numeroVaga': 'A1',
        'valorEstimado': 63.75,
      });
    case '/api/motorista/veiculos':
      return json([
        {'id': 'v1', 'placa': 'ABC1234', 'modelo': 'Onix', 'cor': 'Prata'}
      ]);
    case '/api/motorista/historico':
      return json([]);
    case '/api/dashboard':
      return json({
        'nomePatio': 'Pátio Padrão',
        'hoje': {'entradas': 2, 'saidas': 1, 'receita': 15.0, 'mediaEstadia': '1h 0m'},
        'vagas': resumoVagasJson,
        'ocupacao24h': [
          for (var i = 0; i < 24; i++)
            {'hora': DateTime.utc(2026, 9, 30).add(Duration(hours: i)).toIso8601String(), 'ocupadas': i % 5, 'percentual': 0}
        ],
        'estacionados': [
          {'placa': 'ABC1234', 'dataHoraEntrada': '2026-09-30T10:30:45Z', 'duracao': '4h 15m', 'numeroVaga': 'A1'}
        ],
      });
    case '/api/vagas':
      return json([
        {'id': 'a1', 'numero': 'A1', 'fileira': 'A', 'indice': 1, 'situacao': 'ocupada', 'placaAtual': 'ABC1234'},
        {'id': 'a2', 'numero': 'A2', 'fileira': 'A', 'indice': 2, 'situacao': 'disponivel'},
        {'id': 'a3', 'numero': 'A3', 'fileira': 'A', 'indice': 3, 'situacao': 'manutencao'},
      ]);
  }
  return json({'title': 'Não encontrado'}, 404);
}

Future<void> abrirApp(WidgetTester tester, {required String perfil}) async {
  tester.view.physicalSize = const Size(1400, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  final api = apiSimulada((req) => servidor(req, perfil: perfil));
  final sessao = SessaoProvider(api, ArmazenamentoSessao());
  await sessao.restaurar();

  await tester.pumpWidget(MultiProvider(
    providers: [
      Provider<ClienteApi>.value(value: api),
      ChangeNotifierProvider.value(value: sessao),
      ChangeNotifierProvider(create: (_) => MotoristaProvider(api)),
      ChangeNotifierProvider(create: (_) => GestaoProvider(api)),
    ],
    child: const PkLotsApp(),
  ));
  await tester.pumpAndSettle();
}

Future<void> entrar(WidgetTester tester, String senha) async {
  await tester.enterText(find.widgetWithText(TextFormField, 'E-mail'), 'teste@pklots.local');
  await tester.enterText(find.widgetWithText(TextFormField, 'Senha'), senha);
  await tester.tap(find.text('ENTRAR'));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => initializeDateFormatting('pt_BR'));
  setUp(() => SharedPreferences.setMockInitialValues({}));

  testWidgets('sem sessão abre o login e valida campos vazios', (tester) async {
    await abrirApp(tester, perfil: 'motorista');

    expect(find.text('ENTRAR'), findsOneWidget);
    await tester.tap(find.text('ENTRAR'));
    await tester.pump();

    expect(find.text('Informe o e-mail'), findsOneWidget);
    expect(find.text('Informe a senha'), findsOneWidget);
  });

  testWidgets('senha errada mostra a mensagem da API', (tester) async {
    await abrirApp(tester, perfil: 'motorista');
    await entrar(tester, 'errada');

    expect(find.text('E-mail ou senha inválidos.'), findsOneWidget);
  });

  testWidgets('motorista vê vagas e o estacionamento atual', (tester) async {
    await abrirApp(tester, perfil: 'motorista');
    await entrar(tester, 'Senha@123');

    expect(find.text('Olá, Lucas'), findsOneWidget);
    expect(find.text('28 / 30', findRichText: true), findsOneWidget);
    expect(find.text('ABC-1234'), findsOneWidget);
    expect(find.text('R\$ 63,75'), findsOneWidget);
    expect(find.text('INICIAR SAÍDA'), findsOneWidget);

    // Encerra o timer de atualização automática
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
    expect(find.text('ENTRAR'), findsOneWidget);
  });

  testWidgets('operador vê o painel de gestão', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');

    expect(find.text('Resumo de hoje'), findsOneWidget);
    expect(find.text('Mapa de vagas'), findsOneWidget);
    expect(find.text('Estacionados agora (1)'), findsOneWidget);
    expect(find.text('Entrada e saída'), findsOneWidget);
    expect(find.byTooltip('Vaga A3 · Manutenção'), findsOneWidget);

    await tester.tap(find.text('Ver tabela'));
    await tester.pumpAndSettle();
    expect(find.text('Ocupadas'), findsWidgets);

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('sessão salva é restaurada ao abrir o app', (tester) async {
    SharedPreferences.setMockInitialValues({
      'pklots.token': tokenFalso(nome: 'Ana Souza'),
      'pklots.perfil': 'motorista',
      'pklots.expiraEm': DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
    });

    await abrirApp(tester, perfil: 'motorista');

    expect(find.text('Olá, Ana'), findsOneWidget);
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  test('lê nome e e-mail do payload do token', () {
    final dados = SessaoProvider.lerDadosToken(tokenFalso(nome: 'Lucas Tavares'));
    expect(dados['name'], 'Lucas Tavares');
    expect(SessaoProvider.lerDadosToken('invalido'), isEmpty);
  });
}
