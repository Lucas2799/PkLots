import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:pklots/servicos/excecao_api.dart';

import 'apoio.dart';

void main() {
  test('login envia credenciais e lê o token', () async {
    late http.Request enviada;
    final api = apiSimulada((req) async {
      enviada = req;
      return json({'tokenAcesso': 'abc', 'expiraEm': 86400, 'perfil': 'admin'});
    });

    final token = await api.login('admin@pklots.local', 'Admin@123');

    expect(enviada.url.path, '/api/auth/login');
    expect(jsonDecode(enviada.body), {'email': 'admin@pklots.local', 'senha': 'Admin@123'});
    expect(token.perfil, 'admin');
    expect(token.expiraEm, 86400);
  });

  test('envia o token Bearer nas requisições', () async {
    String? autorizacao;
    final api = apiSimulada((req) async {
      autorizacao = req.headers['Authorization'];
      return json(resumoVagasJson);
    })
      ..token = 'meu-token';

    final resumo = await api.resumoVagas();

    expect(autorizacao, 'Bearer meu-token');
    expect(resumo.disponiveis, 28);
    expect(resumo.percentualDisponivel, closeTo(0.933, 0.001));
  });

  test('204 em estacionamento atual significa nenhum veículo', () async {
    final api = apiSimulada((_) async => http.Response('', 204));
    expect(await api.estacionamentoAtual(), isNull);
  });

  test('erro da API usa o detail do ProblemDetails', () async {
    final api = apiSimulada((_) async =>
        json({'title': 'Conflito', 'status': 409, 'detail': 'O veículo ABC1234 já está estacionado.'}, 409));

    expect(
      () => api.registrarEntrada('ABC1234'),
      throwsA(isA<ExcecaoApi>()
          .having((e) => e.status, 'status', 409)
          .having((e) => e.mensagem, 'mensagem', 'O veículo ABC1234 já está estacionado.')),
    );
  });

  test('401 com token avisa que a sessão expirou', () async {
    var expirou = false;
    final api = apiSimulada((_) async => http.Response('', 401))
      ..token = 'vencido'
      ..aoExpirarSessao = () => expirou = true;

    await expectLater(api.dashboard(), throwsA(isA<ExcecaoApi>()));
    expect(expirou, isTrue);
  });

  test('falha de rede vira mensagem amigável', () async {
    final api = apiSimulada((_) async => throw Exception('sem rede'));
    expect(
      () => api.resumoVagas(),
      throwsA(isA<ExcecaoApi>().having((e) => e.mensagem, 'mensagem', contains('conectar'))),
    );
  });

  test('dashboard converte todas as seções', () async {
    final api = apiSimulada((_) async => json({
          'nomePatio': 'Pátio Padrão',
          'hoje': {'entradas': 156, 'saidas': 154, 'receita': 2847.5, 'mediaEstadia': '2h 34m'},
          'vagas': resumoVagasJson,
          'ocupacao24h': [
            {'hora': '2026-09-30T13:00:00Z', 'ocupadas': 26, 'percentual': 86.7}
          ],
          'estacionados': [
            {'entradaId': 'e1', 'placa': 'ABC1234', 'dataHoraEntrada': '2026-09-30T10:30:45Z', 'duracao': '4h 15m', 'numeroVaga': 'A1'}
          ],
        }));

    final dashboard = await api.dashboard();

    expect(dashboard.hoje.receita, 2847.5);
    expect(dashboard.ocupacao24h.single.ocupadas, 26);
    expect(dashboard.estacionados.single.numeroVaga, 'A1');
  });
}
