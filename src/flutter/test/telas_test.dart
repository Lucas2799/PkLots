import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';
import 'package:pklots/app.dart';
import 'package:pklots/estado/administracao_provider.dart';
import 'package:pklots/estado/gestao_provider.dart';
import 'package:pklots/estado/motorista_provider.dart';
import 'package:pklots/estado/sessao_provider.dart';
import 'package:pklots/servicos/armazenamento_sessao.dart';
import 'package:pklots/servicos/cliente_api.dart';
import 'package:pklots/telas/admin/administracao_tela.dart';
import 'package:pklots/telas/admin/aba_preco.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'apoio.dart';

/// Requisições de escrita recebidas pelo servidor simulado (para conferir o que foi enviado)
final enviadas = <http.Request>[];

/// Todas as requisições (inclusive GET), para conferir cabeçalhos
final recebidas = <http.Request>[];

/// Caixa simulado do usuário (fechado no início de cada teste)
bool caixaAberto = false;

Map<String, dynamic> caixaJson() => {
      'id': 'cx1', 'patioId': 'p1', 'nomePatio': 'Pátio Padrão', 'operadorId': 'u1', 'nomeOperador': 'Lucas Tavares',
      'abertoEm': '2026-09-30T11:00:00Z', 'trocoInicial': 50.0, 'recebimentos': [], 'saidas': 0, 'totalRecebido': 0.0,
      'suprimentos': 0.0, 'sangrias': 0.0, 'dinheiroEsperado': 50.0, 'movimentos': [],
    };

/// Servidor simulado com as rotas usadas pelas telas
Future<http.Response> servidor(http.Request req, {required String perfil}) async {
  recebidas.add(req);
  if (req.method != 'GET') enviadas.add(req);
  switch (req.url.path) {
    case '/api/caixa/atual':
      return caixaAberto ? json(caixaJson()) : http.Response('', 204);
    case '/api/caixa/abrir':
      caixaAberto = true;
      return json(caixaJson());
    case '/api/caixa/':
      return json(caixaAberto ? [caixaJson()] : []);
    case '/api/saidas/cobranca':
      final corpo = jsonDecode(req.body) as Map<String, dynamic>;
      if ((corpo['codigoTicket'] as String? ?? '').contains('USADO')) {
        return json({'title': 'Conflito', 'detail': 'Este ticket já foi usado: a saída de ABC1234 foi registrada em 30/09 às 14:45.'}, 409);
      }
      return json({
        'id': 'c1', 'entradaId': 'e1', 'placa': 'ABC1234', 'numeroVaga': 'A1', 'nomePatio': 'Pátio Padrão',
        'dataHoraEntrada': '2026-09-30T13:30:45Z', 'calculadoEm': '2026-09-30T17:45:30Z', 'expiraEm': '2026-09-30T18:00:30Z',
        'duracaoFormatada': '4h 15m', 'valorHora': 15.0, 'valor': 63.75, 'situacaoPagamento': 'calculado',
        'requerPagamento': true, 'formasAceitas': ['dinheiro', 'pix', 'cartao'],
        'pixCopiaECola': '00020126360014br.gov.bcb.pix0114+55119999988885204000053039865405' '63.755802BR5909PATIO SA6009SAO PAULO62070503***6304ABCD',
      });
    case '/api/saidas/cobranca/c1/confirmar':
      final corpo = jsonDecode(req.body) as Map<String, dynamic>;
      return json({
        'id': 's1', 'entradaVeiculoId': 'e1', 'placa': 'ABC1234',
        'dataHoraEntrada': '2026-09-30T13:30:45Z', 'dataHoraSaida': '2026-09-30T17:45:30Z',
        'duracaoFormatada': '4h 15m', 'valorHora': 15.0, 'valorTotal': 63.75, 'situacaoPagamento': 'pago',
        'numeroVaga': 'A1', 'nomePatio': 'Pátio Padrão', 'formaPagamento': corpo['formaPagamento'],
        'valorRecebido': corpo['valorRecebido'], 'troco': corpo['valorRecebido'] == null ? null : (corpo['valorRecebido'] as num) - 63.75,
      });
    case '/api/mensalistas':
      if (req.method == 'POST') {
        final corpo = jsonDecode(req.body) as Map<String, dynamic>;
        return json({
          'id': 'm9',
          'nome': corpo['nome'],
          'placas': corpo['placas'],
          'situacao': 'vencido',
          'ativo': true,
          'estacionado': false,
        }, 201);
      }
      final filtro = req.url.queryParameters['situacao'];
      final todos = [
        {'id': 'm1', 'nome': 'João Silva', 'placas': ['ABC1234'], 'validoAte': '2026-10-29', 'situacao': 'em_dia', 'diasParaVencer': 28, 'ativo': true, 'estacionado': true},
        {'id': 'm2', 'nome': 'Maria Souza', 'placas': ['BRA2E19'], 'validoAte': '2026-09-20', 'situacao': 'vencido', 'diasParaVencer': -10, 'ativo': true, 'estacionado': false},
      ];
      return json(filtro == null ? todos : todos.where((m) => m['situacao'] == filtro).toList());
    case '/api/mensalistas/m2/pagamentos':
    case '/api/mensalistas/m9/pagamentos':
      if (req.method == 'POST') {
        return json({'id': 'm2', 'nome': 'Maria Souza', 'placas': ['BRA2E19'], 'validoAte': '2026-12-29', 'situacao': 'em_dia', 'diasParaVencer': 89, 'ativo': true, 'estacionado': false});
      }
      return json([]);
    case '/api/patios':
      return json([
        {'id': 'p1', 'nome': 'Pátio Padrão'},
        {'id': 'p2', 'nome': 'Pátio Shopping'},
      ]);
    case '/api/admin/patios':
      if (req.method == 'POST') {
        return json({'id': 'p3', 'nome': 'Pátio Novo', 'ativo': true, 'totalVagas': 20, 'valorHora': 12.0, 'veiculosEstacionados': 0}, 201);
      }
      return json([
        {'id': 'p1', 'nome': 'Pátio Padrão', 'ativo': true, 'totalVagas': 30, 'valorHora': 15.0, 'veiculosEstacionados': 1},
        {'id': 'p2', 'nome': 'Pátio Shopping', 'ativo': true, 'totalVagas': 10, 'valorHora': 12.0, 'veiculosEstacionados': 0},
      ]);
    case '/api/motorista/patios':
      return json([
        {'id': 'p1', 'nome': 'Pátio Padrão', 'nomeEmpresa': 'Empresa Padrão', 'disponiveis': 28, 'total': 30},
        {'id': 'p9', 'nome': 'Pátio Centro', 'nomeEmpresa': 'Estacionamentos Silva', 'disponiveis': 0, 'total': 40},
      ]);
    case '/api/plataforma/empresas':
      if (req.method == 'POST') {
        return json({'id': 'e9', 'nome': 'Estacionamentos Silva', 'ativa': true, 'patios': 1, 'usuarios': 1, 'criadoEm': '2026-10-01T12:00:00Z'}, 201);
      }
      return json([
        {'id': 'e1', 'nome': 'Empresa Padrão', 'documento': null, 'ativa': true, 'patios': 2, 'usuarios': 3, 'criadoEm': '2026-09-30T12:00:00Z'},
      ]);
    case '/api/admin/configuracao':
      return json({
        'nome': 'Pátio Padrão',
        'valorHora': 15.0,
        'minutosTolerancia': req.method == 'PUT' ? 15 : 0,
        'totalVagas': 30,
        'vagasPorFileira': 10,
        'maximoVagas': 260,
      });
    case '/api/admin/usuarios':
      if (req.method == 'POST') {
        final corpo = jsonDecode(req.body) as Map<String, dynamic>;
        return json({
          'id': 'u9',
          'email': corpo['email'],
          'nomeCompleto': corpo['nomeCompleto'],
          'perfil': corpo['perfil'],
          'ativo': true,
          'patiosIds': corpo['patiosIds'],
        }, 201);
      }
      return json([
        {'id': 'u1', 'email': 'teste@pklots.local', 'nomeCompleto': 'Lucas Tavares', 'perfil': 'admin', 'ativo': true, 'patiosIds': []},
        {'id': 'u2', 'email': 'op@pklots.local', 'nomeCompleto': 'Operador Um', 'perfil': 'operador', 'ativo': false, 'patiosIds': ['p2']},
      ]);
    case '/api/auth/trocar-senha':
      final corpo = jsonDecode(req.body) as Map<String, dynamic>;
      if (corpo['senhaAtual'] != 'Temporaria1') {
        return json({'title': 'Requisição inválida', 'detail': 'A senha atual não confere.'}, 400);
      }
      return json({'tokenAcesso': tokenFalso(nome: 'Lucas Tavares', perfil: perfil), 'expiraEm': 86400, 'perfil': perfil, 'deveTrocarSenha': false});
    case '/api/auth/login':
      if (req.body.contains('Temporaria1')) {
        return json({'tokenAcesso': tokenFalso(nome: 'Lucas Tavares', perfil: perfil), 'expiraEm': 86400, 'perfil': perfil, 'deveTrocarSenha': true});
      }
      final senhaCorreta = req.body.contains('Senha@123');
      return senhaCorreta
          ? json({'tokenAcesso': tokenFalso(nome: 'Lucas Tavares', perfil: perfil), 'expiraEm': 86400, 'perfil': perfil})
          : json({'title': 'Não autorizado', 'detail': 'E-mail ou senha inválidos.'}, 401);
    case '/api/vagas/resumo':
      return json(resumoVagasJson);
    case '/api/motorista/contratos':
      return json([
        {
          'id': 'c1',
          'nomePatio': 'Pátio Padrão',
          'nomeEmpresa': 'Empresa Padrão',
          'placas': ['ABC1234'],
          'validoAte': '2026-10-30',
          'situacao': 'vencido',
          'diasParaVencer': -1,
          'estacionados': [
            {'placa': 'ABC1234', 'dataHoraEntrada': '2026-09-30T10:30:45Z', 'duracao': '4h 15m', 'numeroVaga': 'A1', 'valorEstimado': 63.75}
          ],
        }
      ]);
    case '/api/auth/esqueci-senha':
      return http.Response('', 204);
    case '/api/publico/convites/convite-valido':
      return json({
        'nomeMensalista': 'Ana Souza',
        'nomePatio': 'Pátio Padrão',
        'email': 'ana@exemplo.com',
        'placasMascaradas': ['ABC-**34'],
        'contaExistente': false,
      });
    case '/api/publico/convites/convite-valido/aceitar':
      return json({
        'contaCriada': true,
        'token': {'tokenAcesso': tokenFalso(nome: 'Ana Souza'), 'expiraEm': 86400, 'perfil': 'motorista'},
      });
    case '/api/publico/convites-usuario/ativar-valido':
      return json({
        'nomeCompleto': 'Bruno Lima',
        'email': 'bruno@pklots.local',
        'nomeEmpresa': 'Empresa Padrão',
        'perfil': 'operador',
        'patios': ['Pátio Shopping'],
      });
    case '/api/auth/ativar-conta':
      return json({'tokenAcesso': tokenFalso(nome: 'Bruno Lima', perfil: 'operador'), 'expiraEm': 86400, 'perfil': 'operador'});
    case '/api/publico/convites/convite-usado':
      return json({'title': 'Não encontrado', 'detail': 'Convite inválido, expirado ou já utilizado.'}, 404);
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
          {'entradaId': 'e1', 'placa': 'ABC1234', 'dataHoraEntrada': '2026-09-30T10:30:45Z', 'duracao': '4h 15m', 'numeroVaga': 'A1'}
        ],
        'mensalistas': {
          'ativos': 2, 'emDia': 1, 'vencendo': 0, 'vencidos': 1, 'estacionados': 1,
          'vagasReservadas': 5, 'reservadasLivres': 4, 'valorMensalidade': 300.0,
        },
      });
    case '/api/entradas/':
      if (req.body.contains('BRA2E19')) {
        return json({
          'id': 'e3',
          'placa': 'BRA2E19',
          'dataHoraEntrada': '2026-09-30T14:00:00Z',
          'numeroVaga': 'A3',
          'confianca': 1,
          'situacao': 'ativa',
          'mensalista': 'Maria Souza',
          'situacaoMensalista': 'vencido',
          'mensalidadeValidaAte': '2026-09-20',
        }, 201);
      }
      return json({
        'id': 'e2',
        'placa': 'XYZ9876',
        'dataHoraEntrada': '2026-09-30T14:00:00Z',
        'numeroVaga': 'A2',
        'confianca': 1,
        'situacao': 'ativa',
        'impressao': 'impresso',
      }, 201);
    case '/api/admin/impressao':
      final corpo = req.method == 'PUT' ? jsonDecode(req.body) as Map<String, dynamic> : null;
      return json(corpo ??
          {'modo': 'desativada', 'impressoraHost': null, 'impressoraPorta': 9100, 'larguraPapelMm': 80, 'imprimirEntradaAutomaticamente': true});
    case '/api/vagas':
      return json([
        {'id': 'a1', 'numero': 'A1', 'fileira': 'A', 'indice': 1, 'situacao': 'ocupada', 'placaAtual': 'ABC1234'},
        {'id': 'a2', 'numero': 'A2', 'fileira': 'A', 'indice': 2, 'situacao': 'disponivel'},
        {'id': 'a3', 'numero': 'A3', 'fileira': 'A', 'indice': 3, 'situacao': 'manutencao'},
      ]);
  }
  return json({'title': 'Não encontrado'}, 404);
}

Future<void> abrirApp(WidgetTester tester,
    {required String perfil, Size tela = const Size(1400, 2400), Uri? endereco}) async {
  tester.view.physicalSize = tela;
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
      ChangeNotifierProvider(create: (_) => AdministracaoProvider(api)),
    ],
    child: PkLotsApp(enderecoInicial: endereco),
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
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    enviadas.clear();
    recebidas.clear();
    caixaAberto = false;
  });

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

  testWidgets('mensalista vê o contrato, o veículo estacionado e as vagas', (tester) async {
    await abrirApp(tester, perfil: 'motorista');
    await entrar(tester, 'Senha@123');

    expect(find.text('Olá, Lucas'), findsOneWidget);
    expect(find.text('Pátio Padrão'), findsWidgets); // contrato e lista de vagas
    expect(find.text('Vencida'), findsOneWidget);
    expect(find.textContaining('cobrada como avulsa'), findsOneWidget);
    expect(find.text('Estacionado agora'), findsOneWidget);
    expect(find.text('R\$ 63,75'), findsOneWidget);
    expect(find.text('28 / 30', findRichText: true), findsOneWidget);
    expect(find.text('Lotado'), findsOneWidget);
    // Sem cadastro de veículos pelo app: o acesso vem do convite do pátio
    expect(find.text('Cadastrar veículo'), findsNothing);

    // Encerra o timer de atualização automática
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
    expect(find.text('ENTRAR'), findsOneWidget);
  });

  testWidgets('no celular as ações ficam na barra inferior e o restante no "Mais"', (tester) async {
    await abrirApp(tester, perfil: 'admin', tela: const Size(400, 860));
    await entrar(tester, 'Senha@123');

    // Topo sem os ícones; barra inferior com as ações principais
    expect(find.byTooltip('Administração'), findsNothing);
    expect(find.byTooltip('Sair'), findsNothing);
    final barra = find.byType(NavigationBar);
    for (final rotulo in ['Painel', 'Caixa', 'Mensalistas', 'Mais']) {
      expect(find.descendant(of: barra, matching: find.text(rotulo)), findsOneWidget);
    }

    // "Mais" reúne administração, histórico, senha e sair
    await tester.tap(find.descendant(of: barra, matching: find.text('Mais')));
    await tester.pumpAndSettle();
    for (final opcao in ['Administração', 'Histórico por placa', 'Alterar senha', 'Sair']) {
      expect(find.widgetWithText(ListTile, opcao), findsOneWidget);
    }
    await tester.tap(find.widgetWithText(ListTile, 'Administração'));
    await tester.pumpAndSettle();
    expect(find.byType(AdministracaoTela), findsOneWidget);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    await tester.tap(find.descendant(of: barra, matching: find.text('Mais')));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, 'Sair'));
    await tester.pumpAndSettle();
    expect(find.text('ENTRAR'), findsOneWidget);
  });

  testWidgets('operador no celular não vê a administração no "Mais"', (tester) async {
    await abrirApp(tester, perfil: 'operador', tela: const Size(400, 860));
    await entrar(tester, 'Senha@123');

    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text('Mais')));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(ListTile, 'Administração'), findsNothing);
    await tester.tap(find.widgetWithText(ListTile, 'Sair'));
    await tester.pumpAndSettle();
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
    // Operador não vê a administração
    expect(find.byTooltip('Administração'), findsNothing);

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('login não tem cadastro público e pede o link de nova senha', (tester) async {
    await abrirApp(tester, perfil: 'motorista');

    expect(find.text('Criar conta'), findsNothing);
    await tester.enterText(find.widgetWithText(TextFormField, 'E-mail'), 'ana@exemplo.com');
    await tester.tap(find.text('Esqueci minha senha'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Enviar link'));
    await tester.pumpAndSettle();

    expect(enviadas.any((r) => r.url.path == '/api/auth/esqueci-senha' && r.body.contains('ana@exemplo.com')), isTrue);
    expect(find.textContaining('Se houver uma conta com ana@exemplo.com'), findsOneWidget);
  });

  testWidgets('link do convite cria a conta e entra no app', (tester) async {
    await abrirApp(tester, perfil: 'motorista', endereco: Uri.parse('https://pklots.teste/?convite=convite-valido'));

    expect(find.text('Olá, Ana!'), findsOneWidget);
    expect(find.text('ABC-**34'), findsOneWidget);
    await tester.enterText(find.widgetWithText(TextFormField, 'Senha'), 'curta');
    await tester.tap(find.text('ATIVAR MEU ACESSO'));
    await tester.pump();
    expect(find.text('Mínimo de 8 caracteres'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextFormField, 'Senha'), 'Senha@1234');
    await tester.enterText(find.widgetWithText(TextFormField, 'Repita a senha'), 'Senha@1234');
    await tester.tap(find.text('ATIVAR MEU ACESSO'));
    await tester.pumpAndSettle();

    expect(find.text('Olá, Ana'), findsOneWidget); // já dentro do app
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('convite já usado mostra o aviso e leva ao login', (tester) async {
    await abrirApp(tester, perfil: 'motorista', endereco: Uri.parse('https://pklots.teste/?convite=convite-usado'));

    expect(find.text('Convite indisponível'), findsOneWidget);
    await tester.tap(find.text('IR PARA O LOGIN'));
    await tester.pumpAndSettle();
    expect(find.text('ENTRAR'), findsOneWidget);
  });

  testWidgets('convite de operador mostra empresa e pátios, cria a senha e entra no painel', (tester) async {
    await abrirApp(tester, perfil: 'operador', endereco: Uri.parse('https://pklots.teste/?ativar=ativar-valido'));

    expect(find.text('Olá, Bruno!'), findsOneWidget);
    expect(find.textContaining('Empresa Padrão como Operador(a)'), findsOneWidget);
    expect(find.text('Pátio Shopping'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextFormField, 'Senha'), 'Minha@2026');
    await tester.enterText(find.widgetWithText(TextFormField, 'Repita a senha'), 'Minha@2026');
    await tester.tap(find.text('CRIAR SENHA E ENTRAR'));
    await tester.pumpAndSettle();

    final corpo = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/auth/ativar-conta').body) as Map<String, dynamic>;
    expect(corpo, {'token': 'ativar-valido', 'senha': 'Minha@2026'});
    expect(find.text('CRIAR SENHA E ENTRAR'), findsNothing); // saiu da tela de ativação
    await tester.tap(find.byTooltip('Sair').first);
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

  testWidgets('admin abre a administração, simula e salva a tolerância', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    expect(find.text('Configurando: Pátio Padrão'), findsOneWidget);
    await tester.tap(find.text('Preço'));
    await tester.pumpAndSettle();

    expect(find.text('Pátio e preço'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, '15,00'), findsOneWidget);

    // Sem tolerância, 10 minutos custam R$ 2,50; com 15 min de tolerância, ficam grátis
    expect(find.text('R\$\u00a02,50'), findsOneWidget);
    await tester.tap(find.text('15 min'));
    await tester.pumpAndSettle();
    expect(find.text('Grátis'), findsNWidgets(1)); // só "Ficou 10 min"; 16 min já é cobrado

    await tester.ensureVisible(find.text('Salvar'));
    await tester.tap(find.text('Salvar'));
    await tester.pumpAndSettle();

    final put = enviadas.singleWhere((r) => r.url.path == '/api/admin/configuracao');
    expect(put.method, 'PUT');
    expect(put.body, contains('"minutosTolerancia":15'));
    expect(put.body, contains('"valorHora":15.0'));
    expect(find.textContaining('Configuração salva'), findsOneWidget);
  });

  testWidgets('admin vê usuários e o próprio usuário marcado', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');
    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Usuários'));
    await tester.pumpAndSettle();

    expect(find.text('Lucas Tavares (você)'), findsOneWidget);
    expect(find.textContaining('Inativo'), findsOneWidget);
    expect(find.text('Novo usuário'), findsOneWidget);

    // Na própria conta, perfil e desativação ficam bloqueados
    await tester.tap(find.text('Lucas Tavares (você)'));
    await tester.pumpAndSettle();
    expect(find.text('Você não pode desativar a própria conta'), findsOneWidget);
  });

  testWidgets('entrada com impressão automática avisa que o ticket foi impresso', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');

    await tester.enterText(find.widgetWithText(TextFormField, 'Placa'), 'XYZ9876');
    await tester.tap(find.widgetWithText(FilledButton, 'Entrada'));
    await tester.pumpAndSettle();

    expect(find.textContaining('ticket impresso'), findsOneWidget);
    expect(find.byTooltip('Reimprimir ticket de ABC1234'), findsOneWidget);

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('saída pelo ticket: cobra, exige caixa, calcula troco e mostra o recibo', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');
    expect(find.text('Caixa fechado: abra para receber pagamentos'), findsOneWidget);

    // Leitor USB: "digita" o conteúdo do QR e envia Enter
    await tester.enterText(find.widgetWithText(TextField, 'Ler ticket'), 'PKLOTS:K7M2Q9XD');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();

    final cobranca = enviadas.singleWhere((r) => r.url.path == '/api/saidas/cobranca');
    expect(jsonDecode(cobranca.body), {'codigoTicket': 'PKLOTS:K7M2Q9XD'});
    expect(find.text('Saída · ABC-1234'), findsOneWidget);
    expect(find.text('R\$\u00a063,75'), findsOneWidget);

    // Caixa fechado bloqueia o recebimento
    expect(find.text('Seu caixa está fechado. Abra o caixa para receber.'), findsOneWidget);
    await tester.tap(find.widgetWithText(OutlinedButton, 'Abrir caixa'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Troco inicial'), '50,00');
    await tester.tap(find.text('Confirmar'));
    await tester.pumpAndSettle();
    expect(find.text('Seu caixa está fechado. Abra o caixa para receber.'), findsNothing);

    // PIX mostra o QR; dinheiro calcula o troco
    await tester.tap(find.text('PIX'));
    await tester.pumpAndSettle();
    expect(find.text('Copiar PIX copia e cola'), findsOneWidget);
    await tester.tap(find.text('Dinheiro'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Valor recebido'), '100,00');
    await tester.pump();
    expect(find.text('Troco: R\$\u00a036,25'), findsOneWidget);

    await tester.tap(find.text('Receber e liberar'));
    await tester.pumpAndSettle();

    final confirmacao = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/saidas/cobranca/c1/confirmar').body);
    expect(confirmacao, {'formaPagamento': 'dinheiro', 'valorRecebido': 100.0});
    expect(find.text('Saída registrada'), findsOneWidget);
    expect(find.text('Dinheiro'), findsOneWidget);
    expect(find.text('R\$\u00a036,25'), findsOneWidget); // troco no recibo
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();

    // Ticket já usado: aviso da API, nenhuma cobrança aberta
    await tester.enterText(find.widgetWithText(TextField, 'Ler ticket'), 'USADO123');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
    expect(find.textContaining('já foi usado'), findsOneWidget);

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('admin configura a impressora de rede', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');
    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Impressão'));
    await tester.pumpAndSettle();

    // Desativada: sem campos de impressora
    expect(find.text('IP da impressora'), findsNothing);

    await tester.tap(find.text('Impressora de rede'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'IP da impressora'), '192.168.0.50');
    await tester.tap(find.text('58 mm'));
    await tester.pumpAndSettle();
    // Teste da rede só depois de salvar
    expect(find.text('Salve para testar a impressora de rede.'), findsOneWidget);

    await tester.ensureVisible(find.text('Salvar'));
    await tester.tap(find.text('Salvar'));
    await tester.pumpAndSettle();

    final put = enviadas.singleWhere((r) => r.url.path == '/api/admin/impressao');
    final corpo = jsonDecode(put.body) as Map<String, dynamic>;
    expect(corpo['modo'], 'rede');
    expect(corpo['impressoraHost'], '192.168.0.50');
    expect(corpo['larguraPapelMm'], 58);
    expect(find.textContaining('Configuração de impressão salva'), findsOneWidget);
  });

  testWidgets('trocar de pátio envia o novo pátio em todas as requisições', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');

    // Começa no primeiro pátio permitido
    expect(recebidas.where((r) => r.url.path == '/api/dashboard').last.headers['X-Patio-Id'], 'p1');

    await tester.tap(find.byTooltip('Trocar de pátio'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Pátio Shopping').last);
    await tester.pumpAndSettle();

    expect(recebidas.where((r) => r.url.path == '/api/dashboard').last.headers['X-Patio-Id'], 'p2');
    expect(find.text('· Pátio Shopping'), findsOneWidget);
    // A escolha fica salva no aparelho
    expect((await SharedPreferences.getInstance()).getString('pklots.patio'), 'p2');

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('admin convida operador vinculado a pátios (sem definir senha)', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');
    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Usuários'));
    await tester.pumpAndSettle();

    // Operador mostra os pátios em que trabalha
    expect(find.textContaining('Pátio Shopping'), findsOneWidget);

    await tester.tap(find.text('Novo usuário'));
    await tester.pumpAndSettle();
    expect(find.text('Motorista'), findsNothing); // mensalistas entram por convite
    await tester.enterText(find.widgetWithText(TextFormField, 'Nome completo'), 'Ana Operadora');
    await tester.enterText(find.widgetWithText(TextFormField, 'E-mail'), 'ana@pklots.local');
    expect(find.widgetWithText(TextFormField, 'Senha inicial'), findsNothing);
    expect(find.textContaining('recebe um convite por e-mail'), findsOneWidget);

    // Sem pátio marcado não salva
    await tester.tap(find.widgetWithText(FilledButton, 'Convidar'));
    await tester.pumpAndSettle();
    expect(find.text('Selecione pelo menos um pátio para o operador.'), findsOneWidget);

    await tester.tap(find.widgetWithText(CheckboxListTile, 'Pátio Shopping'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Convidar'));
    await tester.pumpAndSettle();

    final post = enviadas.singleWhere((r) => r.url.path == '/api/admin/usuarios');
    final corpo = jsonDecode(post.body) as Map<String, dynamic>;
    expect(corpo['perfil'], 'operador');
    expect(corpo['patiosIds'], ['p2']);
    expect(corpo.containsKey('senha'), isFalse);
  });

  testWidgets('superadmin vê as empresas e cria uma nova', (tester) async {
    await abrirApp(tester, perfil: 'superadmin');
    await entrar(tester, 'Senha@123');

    expect(find.text('· Plataforma'), findsOneWidget);
    expect(find.text('Empresa Padrão'), findsOneWidget);
    expect(find.byTooltip('Administração'), findsNothing);

    await tester.tap(find.text('Nova empresa'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Nome da empresa'), 'Estacionamentos Silva');
    await tester.enterText(find.widgetWithText(TextFormField, 'Nome completo'), 'Maria Silva');
    await tester.enterText(find.widgetWithText(TextFormField, 'E-mail (login)'), 'maria@silva.com');
    expect(find.widgetWithText(TextFormField, 'Senha inicial'), findsNothing);
    await tester.tap(find.text('Criar empresa'));
    await tester.pumpAndSettle();

    final corpo = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/plataforma/empresas').body) as Map<String, dynamic>;
    expect(corpo['nome'], 'Estacionamentos Silva');
    expect(corpo['nomePatio'], 'Pátio Principal');
    expect(corpo['totalVagas'], 30);
    expect(corpo['emailAdmin'], 'maria@silva.com');
    expect(corpo.containsKey('senhaAdmin'), isFalse);

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('operador cadastra mensalista e registra o pagamento', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');

    // Resumo no painel
    expect(find.text('Mensalistas (2)'), findsOneWidget);
    expect(find.text('4 de 5 vagas reservadas livres'), findsOneWidget);

    await tester.tap(find.byTooltip('Mensalistas'));
    await tester.pumpAndSettle();
    expect(find.text('João Silva'), findsOneWidget);
    expect(find.text('Vencido'), findsOneWidget);

    // Filtro
    await tester.tap(find.text('Vencidos'));
    await tester.pumpAndSettle();
    expect(recebidas.last.url.queryParameters['situacao'], 'vencido');
    expect(find.text('João Silva'), findsNothing);

    // Novo mensalista com duas placas
    await tester.tap(find.text('Novo mensalista'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Nome'), 'Pedro Lima');
    await tester.enterText(find.widgetWithText(TextField, 'Adicionar placa'), 'pqr-1234');
    await tester.tap(find.byTooltip('Adicionar placa'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Adicionar placa'), 'XY');
    await tester.tap(find.byTooltip('Adicionar placa'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Placa inválida'), findsOneWidget);
    await tester.enterText(find.widgetWithText(TextField, 'Adicionar placa'), 'bra2e20');
    await tester.tap(find.widgetWithText(FilledButton, 'Salvar')); // placa digitada entra ao salvar
    await tester.pumpAndSettle();

    final post = enviadas.singleWhere((r) => r.url.path == '/api/mensalistas');
    expect((jsonDecode(post.body) as Map)['placas'], ['PQR1234', 'BRA2E20']);

    // Abre o detalhe e registra 3 meses: valor sugerido = 3 × mensalidade
    expect(find.text('Registrar pagamento'), findsOneWidget);
    await tester.tap(find.text('Registrar pagamento'));
    await tester.pumpAndSettle();
    expect(find.text('Mensalidade do pátio: R\$\u00a0300,00'), findsOneWidget);
    await tester.tap(find.text('1 mês'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('3 meses').last);
    await tester.pumpAndSettle();
    expect(find.widgetWithText(TextField, '900,00'), findsOneWidget);
    await tester.tap(find.text('Dinheiro'));
    await tester.tap(find.widgetWithText(FilledButton, 'Registrar'));
    await tester.pumpAndSettle();

    final pagamento = jsonDecode(enviadas.last.body) as Map<String, dynamic>;
    expect((pagamento['meses'], pagamento['valor'], pagamento['formaPagamento']), (3, 900.0, 'dinheiro'));
    expect(find.textContaining('Válida até 29/12/2026'), findsOneWidget);
  });

  testWidgets('entrada de mensalista vencido avisa que será cobrado como avulso', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');

    await tester.enterText(find.widgetWithText(TextFormField, 'Placa'), 'BRA2E19');
    await tester.tap(find.widgetWithText(FilledButton, 'Entrada'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Mensalidade de Maria Souza vencida: será cobrado como avulso'), findsOneWidget);
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('admin configura mensalidade e vagas reservadas', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');
    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Preço'));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextFormField, 'Mensalidade'), '280,00');
    await tester.enterText(find.widgetWithText(TextFormField, 'Vagas reservadas'), '8');
    await tester.ensureVisible(find.text('Salvar'));
    await tester.tap(find.text('Salvar'));
    await tester.pumpAndSettle();

    final corpo = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/admin/configuracao').body) as Map<String, dynamic>;
    expect((corpo['valorMensalidade'], corpo['vagasReservadasMensalistas']), (280.0, 8));
  });

  testWidgets('senha temporária obriga a trocar antes de usar o sistema', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Temporaria1');

    expect(find.text('Defina sua senha'), findsOneWidget);
    expect(find.text('Resumo de hoje'), findsNothing); // painel bloqueado

    // Validação local: letras e números
    await tester.enterText(find.widgetWithText(TextFormField, 'Senha atual'), 'Temporaria1');
    await tester.enterText(find.widgetWithText(TextFormField, 'Nova senha'), 'somenteletras');
    await tester.enterText(find.widgetWithText(TextFormField, 'Confirmar nova senha'), 'somenteletras');
    await tester.tap(find.text('SALVAR NOVA SENHA'));
    await tester.pump();
    expect(find.text('Use letras e números'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextFormField, 'Nova senha'), 'MinhaNova2026');
    await tester.enterText(find.widgetWithText(TextFormField, 'Confirmar nova senha'), 'MinhaNova2026');
    await tester.tap(find.text('SALVAR NOVA SENHA'));
    await tester.pumpAndSettle();

    final corpo = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/auth/trocar-senha').body);
    expect(corpo, {'senhaAtual': 'Temporaria1', 'novaSenha': 'MinhaNova2026'});
    expect(find.text('Resumo de hoje'), findsOneWidget); // liberado

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  test('lê valores em reais com vírgula ou ponto', () {
    expect(AbaPreco.lerValor('15,50'), 15.5);
    expect(AbaPreco.lerValor('1.234,50'), 1234.5);
    expect(AbaPreco.lerValor('15.50'), 15.5);
    expect(AbaPreco.lerValor('abc'), isNull);
  });

  test('lê nome e e-mail do payload do token', () {
    final dados = SessaoProvider.lerDadosToken(tokenFalso(nome: 'Lucas Tavares'));
    expect(dados['name'], 'Lucas Tavares');
    expect(SessaoProvider.lerDadosToken('invalido'), isEmpty);
  });
}
