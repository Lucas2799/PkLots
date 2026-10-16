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

/// Recursos liberados pelo plano simulado da empresa (todos no início de cada teste)
List<String> recursosLiberados = [];

/// Situação de cobrança simulada da empresa (em dia no início de cada teste)
Map<String, dynamic> situacaoCobranca = {};
const todosRecursos = ['lpr', 'mensalistas', 'app_mensalista', 'relatorios', 'impressora_rede'];

Map<String, dynamic> faturaJson(String id, String situacao, {String? pix, int atraso = 0}) => {
      'id': id, 'empresaId': 'e1', 'nomeEmpresa': 'Empresa Padrão', 'tipo': 'mensalidade', 'competencia': '2026-10-01',
      'descricao': 'Mensalidade 10/2026 · plano Básico', 'valor': 99.0, 'vencimento': '2026-10-05', 'situacao': situacao,
      'diasEmAtraso': atraso, 'patiosCobrados': 1, 'precoPorPatio': 99.0, 'nomePlano': 'Básico',
      'pagaEm': situacao == 'paga' ? '2026-09-05T15:00:00Z' : null, 'formaPagamento': situacao == 'paga' ? 'pix' : null,
      'valorPago': situacao == 'paga' ? 99.0 : null, 'observacao': null, 'canceladaEm': null, 'motivoCancelamento': null,
      'pixCopiaECola': pix, 'criadoEm': '2026-09-25T12:00:00Z',
    };

Map<String, dynamic> planoJson(String id, String nome, double preco, {int? patios, List<String> recursos = todosRecursos}) => {
      'id': id, 'nome': nome, 'descricao': null, 'precoPorPatio': preco, 'maximoPatios': patios, 'maximoVagasPorPatio': null,
      'maximoUsuarios': 3, 'recursos': recursos, 'diasCarencia': 7, 'ativo': true, 'empresas': 1,
    };

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
        'requerPagamento': true, 'formasAceitas': ['dinheiro', 'pix', 'cartao'], 'detalhamento': '4h 15m × R\$ 15,00/h',
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
    case '/api/assinatura/recursos':
      return json(recursosLiberados);
    case '/api/assinatura/situacao':
      return json(situacaoCobranca);
    case '/api/admin/assinatura':
      return json({
        'nomePlano': 'Básico', 'descricaoPlano': 'Pátio pequeno', 'precoPorPatio': 99.0, 'valorMensal': 99.0, 'diaVencimento': 10,
        'testeAte': null,
        'patios': {'usado': 1, 'maximo': 1}, 'usuarios': {'usado': 2, 'maximo': 3}, 'vagasNoMaiorPatio': {'usado': 30, 'maximo': null},
        'recursos': [
          {'codigo': 'lpr', 'nome': 'Leitura automática de placa', 'liberado': false},
          {'codigo': 'relatorios', 'nome': 'Relatórios', 'liberado': true},
        ],
      });
    case '/api/plataforma/recursos':
      return json([
        {'codigo': 'lpr', 'nome': 'Leitura automática de placa', 'descricao': 'Entrada e saída pela foto do veículo'},
        {'codigo': 'relatorios', 'nome': 'Relatórios', 'descricao': 'Relatório gerencial por período'},
      ]);
    case '/api/plataforma/planos':
      if (req.method == 'POST') {
        final corpo = jsonDecode(req.body) as Map<String, dynamic>;
        return json({...corpo, 'id': 'pl9', 'empresas': 0}, 201);
      }
      return json([planoJson('pl1', 'Cortesia', 0), planoJson('pl2', 'Básico', 99, patios: 1, recursos: ['relatorios'])]);
    case '/api/plataforma/empresas/e1/assinatura':
      if (req.method == 'PUT') enviadas.add(req);
      return json({
        'empresaId': 'e1', 'planoId': 'pl1', 'nomePlano': 'Cortesia', 'precoPorPatioPlano': 0.0, 'precoPorPatioNegociado': null,
        'diaVencimento': 10, 'testeAte': null, 'observacoes': null, 'patiosAtivos': 2, 'valorMensal': 0.0,
      });
    case '/api/admin/faturas':
      return json([faturaJson('f1', 'vencida', pix: '00020126PIXDOPKLOTS6304ABCD', atraso: 3), faturaJson('f0', 'paga')]);
    case '/api/plataforma/faturas':
      return json([faturaJson('f1', 'vencida', atraso: 3)]);
    case '/api/plataforma/faturas/f1/pagamento':
      return json(faturaJson('f1', 'paga'));
    case '/api/relatorios/empresa':
      final umPatio = req.url.queryParameters['patioId'];
      return json({
        'de': req.url.queryParameters['de'], 'ate': req.url.queryParameters['ate'], 'nomeEmpresa': 'Empresa Padrão',
        'nomePatio': umPatio == null ? null : 'Pátio Shopping',
        'totais': {
          'entradas': 42, 'saidas': 40, 'saidasCobradas': 35, 'saidasIsentas': 3, 'saidasMensalistas': 2, 'cancelamentos': 1,
          'receitaAvulsos': 1250.0, 'receitaMensalidades': 600.0, 'receitaTotal': 1850.0, 'ticketMedio': 35.71, 'permanenciaMedia': '2h 10m',
        },
        'porForma': [
          {'forma': 'pix', 'quantidade': 20, 'total': 1100.0},
          {'forma': 'dinheiro', 'quantidade': 17, 'total': 750.0},
        ],
        'porDia': [
          {'data': '2026-09-29', 'entradas': 20, 'saidas': 19, 'receitaAvulsos': 600.0, 'receitaMensalidades': 0.0, 'receita': 600.0},
          {'data': '2026-09-30', 'entradas': 22, 'saidas': 21, 'receitaAvulsos': 650.0, 'receitaMensalidades': 600.0, 'receita': 1250.0},
        ],
        'entradasPorHora': [for (var h = 0; h < 24; h++) {'hora': h, 'entradas': h == 8 ? 12 : 1}],
        'porPatio': [
          {'id': 'p1', 'nome': 'Pátio Padrão', 'ativo': true, 'vagas': 30, 'entradas': 30, 'saidas': 29, 'receitaAvulsos': 900.0,
            'receitaMensalidades': 600.0, 'giroPorVaga': 0.48},
          {'id': 'p2', 'nome': 'Pátio Shopping', 'ativo': true, 'vagas': 10, 'entradas': 12, 'saidas': 11, 'receitaAvulsos': 350.0,
            'receitaMensalidades': 0.0, 'giroPorVaga': 0.55},
        ],
        'porOperador': [
          {'id': 'u2', 'nome': 'Bruno Lima', 'saidas': 35, 'mensalidades': 2, 'recebido': 1850.0, 'caixasFechados': 2,
            'diferencaCaixa': -5.0, 'cancelamentos': 1},
        ],
        'cancelamentos': [
          {'canceladaEm': '2026-09-30T12:00:00Z', 'placa': 'ABC1234', 'nomePatio': 'Pátio Padrão', 'canceladaPor': 'Bruno Lima',
            'motivo': 'Placa digitada errada'},
        ],
        'mensalistas': {'ativos': 8, 'emDia': 6, 'vencidos': 2, 'novos': 1, 'pagamentos': 2},
      });
    case '/api/plataforma/relatorio':
      return json({
        'de': req.url.queryParameters['de'], 'ate': req.url.queryParameters['ate'],
        'totais': {
          'empresasAtivas': 2, 'empresasNovas': 1, 'empresasSemMovimento': 1, 'patiosAtivos': 3, 'vagas': 70, 'usuariosAtivos': 5,
          'mensalistasAtivos': 8, 'entradas': 42, 'valorProcessado': 1850.0,
        },
        'porDia': [
          {'data': '2026-09-29', 'entradas': 20, 'saidas': 19, 'receitaAvulsos': 600.0, 'receitaMensalidades': 0.0, 'receita': 600.0},
          {'data': '2026-09-30', 'entradas': 22, 'saidas': 21, 'receitaAvulsos': 650.0, 'receitaMensalidades': 600.0, 'receita': 1250.0},
        ],
        'empresas': [
          {'id': 'e1', 'nome': 'Empresa Padrão', 'ativa': true, 'criadoEm': '2026-09-01T12:00:00Z', 'patios': 2, 'vagas': 40,
            'usuarios': 3, 'mensalistasAtivos': 8, 'entradas': 42, 'valorProcessado': 1850.0, 'ultimaEntrada': DateTime.now().toUtc().toIso8601String()},
          {'id': 'e2', 'nome': 'Estacionamentos Silva', 'ativa': true, 'criadoEm': '2026-09-20T12:00:00Z', 'patios': 1, 'vagas': 30,
            'usuarios': 2, 'mensalistasAtivos': 0, 'entradas': 0, 'valorProcessado': 0.0, 'ultimaEntrada': null},
        ],
      });
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
    case '/api/admin/tabela-preco':
      final semana = req.method == 'PUT'
          ? (jsonDecode(req.body) as Map<String, dynamic>)['semana']
          : {'modo': 'hora', 'valorHora': 15.0, 'primeiroPeriodoMinutos': 60, 'valorPrimeiroPeriodo': 0.0, 'fracaoMinutos': 60,
              'valorFracao': 0.0, 'diariaMaxima': null, 'valorPernoite': null, 'inicioPernoite': 20, 'fimPernoite': 8};
      final tolerancia = req.method == 'PUT' ? (jsonDecode(req.body) as Map<String, dynamic>)['minutosTolerancia'] : 0;
      return json({'semana': semana, 'fimDeSemana': null, 'minutosTolerancia': tolerancia, 'descricao': 'R\$ 15,00 por hora'});
    case '/api/admin/tabela-preco/simular':
      return json({'valor': 13.0, 'duracao': '1h 30m', 'isento': false, 'fimDeSemana': false,
        'detalhamento': '1ª hora R\$ 10,00 + 1 × 30 min de R\$ 3,00'});
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
    recursosLiberados = [...todosRecursos];
    situacaoCobranca = {'situacao': 'em_dia', 'diasEmAtraso': 0, 'diasParaBloqueio': 0, 'valorVencido': 0.0};
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

  testWidgets('admin vê o resumo da tabela de preço e salva o pátio', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    expect(find.text('Configurando: Pátio Padrão'), findsOneWidget);
    await tester.tap(find.text('Preço'));
    await tester.pumpAndSettle();

    expect(find.text('Pátio e preço'), findsOneWidget);
    expect(find.text('R\$ 15,00 por hora'), findsOneWidget);
    expect(find.text('Tolerância gratuita: nenhuma'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Tolerância'), findsNothing); // editada na tabela de preço

    await tester.ensureVisible(find.text('Salvar'));
    await tester.tap(find.text('Salvar'));
    await tester.pumpAndSettle();

    final put = enviadas.singleWhere((r) => r.url.path == '/api/admin/configuracao');
    expect(put.method, 'PUT');
    expect(put.body, contains('"minutosTolerancia":0')); // tolerância e valor/hora seguem os do pátio
    expect(put.body, contains('"valorHora":15.0'));
    expect(find.textContaining('Configuração salva'), findsOneWidget);
  });

  testWidgets('admin monta a tabela com 1ª hora + frações e diária, simula e salva', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');
    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Preço'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Editar tabela de preço'));
    await tester.pumpAndSettle();
    expect(find.text('Tabela de preço'), findsOneWidget);
    expect(find.text('Ficou 1h 30min'), findsOneWidget); // simulação inicial

    await tester.tap(find.text('15 min').first);
    await tester.pumpAndSettle();
    expect(jsonDecode(enviadas.lastWhere((r) => r.url.path == '/api/admin/tabela-preco/simular').body)['minutosTolerancia'], 15);

    await tester.tap(find.text('1º período + frações'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Primeiro período'), '60');
    await tester.enterText(find.widgetWithText(TextFormField, 'Valor'), '10,00');
    await tester.enterText(find.widgetWithText(TextFormField, 'Cada fração adicional'), '30');
    await tester.enterText(find.widgetWithText(TextFormField, 'Valor da fração'), '3,00');
    await tester.tap(find.text('Diária máxima'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Diária máxima'), '50,00');
    await tester.tap(find.text('Simular'));
    await tester.pumpAndSettle();

    final simulacao = jsonDecode(enviadas.lastWhere((r) => r.url.path == '/api/admin/tabela-preco/simular').body) as Map<String, dynamic>;
    expect((simulacao['semana'] as Map)['modo'], 'fracao');
    expect((simulacao['semana'] as Map)['diariaMaxima'], 50.0);
    expect(find.text('1ª hora R\$ 10,00 + 1 × 30 min de R\$ 3,00'), findsWidgets);

    await tester.ensureVisible(find.text('Salvar tabela'));
    await tester.tap(find.text('Salvar tabela'));
    await tester.pumpAndSettle();

    final tabela = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/admin/tabela-preco').body) as Map<String, dynamic>;
    final semana = tabela['semana'] as Map<String, dynamic>;
    expect([semana['modo'], semana['primeiroPeriodoMinutos'], semana['valorPrimeiroPeriodo'], semana['fracaoMinutos'], semana['valorFracao']],
        ['fracao', 60, 10.0, 30, 3.0]);
    expect(tabela['fimDeSemana'], isNull);
    expect(tabela['minutosTolerancia'], 15);
    expect(find.textContaining('Tabela de preço salva'), findsOneWidget);
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
    expect(find.text('4h 15m × R\$ 15,00/h'), findsOneWidget); // como o valor foi calculado

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
    await tester.tap(find.text('Criar empresa'));
    await tester.pumpAndSettle();
    expect(find.text('Escolha o plano'), findsOneWidget);
    await tester.tap(find.widgetWithText(DropdownButtonFormField<String>, 'Plano'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Básico · R\$\u00a099,00/pátio').last);
    await tester.pumpAndSettle();
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
    expect(corpo['planoId'], 'pl2');
    expect(corpo.containsKey('senhaAdmin'), isFalse);

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();
  });

  testWidgets('admin abre os relatórios, troca o período e filtra um pátio', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Relatórios'));
    await tester.pumpAndSettle();

    expect(find.text('Receita total'), findsOneWidget);
    expect(find.text('R\$\u00a01.850,00'), findsWidgets);
    expect(find.text('Comparativo dos pátios'), findsOneWidget);
    expect(find.text('Bruno Lima'), findsWidgets);
    await tester.scrollUntilVisible(find.text('Placa digitada errada'), 300, scrollable: find.byType(Scrollable).first);
    expect(find.text('ABC-1234'), findsOneWidget);

    List<http.Request> consultas() => recebidas.where((r) => r.url.path == '/api/relatorios/empresa').toList();
    final primeira = consultas().last.url.queryParameters;
    final hoje = DateTime.now();
    expect(primeira['de'], '${hoje.year}-${hoje.month.toString().padLeft(2, '0')}-01');
    expect(primeira.containsKey('patioId'), isFalse);

    await tester.scrollUntilVisible(find.text('Hoje'), -300, scrollable: find.byType(Scrollable).first);
    await tester.tap(find.text('Hoje'));
    await tester.pumpAndSettle();
    expect(consultas().last.url.queryParameters['de'], consultas().last.url.queryParameters['ate']);

    await tester.tap(find.text('Todos os pátios'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Pátio Shopping').last);
    await tester.pumpAndSettle();
    expect(consultas().last.url.queryParameters['patioId'], 'p2');
  });

  testWidgets('operador não vê o botão de relatórios', (tester) async {
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');

    expect(find.byTooltip('Relatórios'), findsNothing);
  });

  testWidgets('superadmin vê o uso por empresa e quem está parado', (tester) async {
    await abrirApp(tester, perfil: 'superadmin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Relatórios'));
    await tester.pumpAndSettle();

    expect(find.text('Relatório da plataforma'), findsOneWidget);
    expect(find.text('Empresas ativas'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Estacionamentos Silva'), 300, scrollable: find.byType(Scrollable).first);
    expect(find.text('Nunca'), findsOneWidget);
    expect(find.byIcon(Icons.warning_amber_rounded), findsOneWidget); // só a empresa sem movimento
  });

  testWidgets('superadmin cria plano com limites e recursos e troca o plano de uma empresa', (tester) async {
    await abrirApp(tester, perfil: 'superadmin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Planos'));
    await tester.pumpAndSettle();
    expect(find.text('Cortesia'), findsOneWidget);
    expect(find.textContaining('até 1 pátio(s)'), findsOneWidget);

    await tester.tap(find.text('Novo plano'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Nome do plano'), 'Profissional');
    await tester.enterText(find.widgetWithText(TextFormField, 'Preço por pátio/mês'), '199,90');
    await tester.enterText(find.widgetWithText(TextFormField, 'Usuários'), '10');
    await tester.tap(find.text('Leitura automática de placa'));
    await tester.pump();
    await tester.tap(find.text('Salvar'));
    await tester.pumpAndSettle();

    final plano = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/plataforma/planos').body) as Map<String, dynamic>;
    expect(plano['nome'], 'Profissional');
    expect(plano['precoPorPatio'], 199.9);
    expect(plano['maximoPatios'], isNull); // em branco: sem limite
    expect(plano['maximoUsuarios'], 10);
    expect(plano['recursos'], ['lpr']);
    expect(plano['diasCarencia'], 7);

    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Assinatura').first);
    await tester.pumpAndSettle();
    expect(find.textContaining('2 pátio(s) ativo(s)'), findsOneWidget);
    await tester.enterText(find.widgetWithText(TextFormField, 'Preço negociado/pátio'), '50');
    await tester.pump();
    expect(find.textContaining('= R\$\u00a0100,00 por mês'), findsOneWidget);
    await tester.tap(find.text('Salvar'));
    await tester.pumpAndSettle();

    final assinatura =
        jsonDecode(enviadas.lastWhere((r) => r.url.path == '/api/plataforma/empresas/e1/assinatura').body) as Map<String, dynamic>;
    expect(assinatura['planoId'], 'pl1');
    expect(assinatura['precoPorPatioNegociado'], 50);
    expect(assinatura['diaVencimento'], 10);
  });

  testWidgets('superadmin dá baixa numa fatura vencida', (tester) async {
    await abrirApp(tester, perfil: 'superadmin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Faturas'));
    await tester.pumpAndSettle();
    expect(find.text('Vencida há 3 dia(s)'), findsOneWidget);
    expect(recebidas.last.url.queryParameters['situacao'], 'aberta');

    await tester.tap(find.text('Dar baixa'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirmar'));
    await tester.pumpAndSettle();

    final corpo = jsonDecode(enviadas.singleWhere((r) => r.url.path == '/api/plataforma/faturas/f1/pagamento').body) as Map<String, dynamic>;
    expect(corpo['formaPagamento'], 'pix');
    expect(corpo['valorPago'], 99.0);
    expect(corpo['pagaEm'], isNotNull);
  });

  testWidgets('fatura vencida avisa o admin; bloqueio aparece também para o operador', (tester) async {
    situacaoCobranca = {'situacao': 'vencida', 'diasEmAtraso': 2, 'diasParaBloqueio': 6, 'valorVencido': 99.0};
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');
    expect(find.textContaining('novas entradas serão bloqueadas em 6 dia(s)'), findsOneWidget);
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();

    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');
    expect(find.textContaining('Fatura do PkLots vencida'), findsNothing); // operador só vê o bloqueio
    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();

    situacaoCobranca = {'situacao': 'bloqueada', 'diasEmAtraso': 9, 'diasParaBloqueio': 0, 'valorVencido': 99.0};
    await abrirApp(tester, perfil: 'operador');
    await entrar(tester, 'Senha@123');
    expect(find.textContaining('Acesso parcialmente bloqueado'), findsOneWidget);
    expect(find.text('Avise o administrador da empresa.'), findsOneWidget);
  });

  testWidgets('plano sem leitura de placa e sem relatórios esconde esses botões', (tester) async {
    recursosLiberados = ['mensalistas'];
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');

    expect(find.text('ou pela foto da placa'), findsNothing);
    expect(find.byTooltip('Relatórios'), findsNothing);
    expect(find.text('Entrada'), findsOneWidget); // entrada por placa digitada continua
  });

  testWidgets('admin vê o plano, o uso dos limites e os recursos', (tester) async {
    await abrirApp(tester, perfil: 'admin');
    await entrar(tester, 'Senha@123');

    await tester.tap(find.byTooltip('Administração'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Assinatura'));
    await tester.pumpAndSettle();

    expect(find.text('Plano Básico'), findsOneWidget);
    expect(find.text('Vencida há 3 dia(s)'), findsOneWidget);
    expect(find.text('Paga'), findsOneWidget);
    await tester.tap(find.text('Pagar com PIX'));
    await tester.pumpAndSettle();
    expect(find.bySemanticsLabel('QR Code PIX da fatura'), findsOneWidget);
    await tester.tap(find.text('Fechar'));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('1 de 1'), 300, scrollable: find.byType(Scrollable).last);
    expect(find.text('1 de 1'), findsOneWidget);
    expect(find.text('30 · sem limite'), findsOneWidget);
    expect(find.text('Não incluído'), findsOneWidget);
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
