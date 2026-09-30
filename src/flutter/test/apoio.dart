import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:pklots/servicos/cliente_api.dart';

/// Monta um JWT falso (a assinatura não é validada pelo app)
String tokenFalso({String nome = 'Motorista Exemplo', String perfil = 'motorista'}) {
  String parte(Map<String, dynamic> dados) => base64Url.encode(utf8.encode(jsonEncode(dados))).replaceAll('=', '');
  return '${parte({'alg': 'HS256'})}.${parte({'name': nome, 'email': 'teste@pklots.local', 'role': perfil})}.assinatura';
}

http.Response json(Object? corpo, [int status = 200]) => http.Response.bytes(
      utf8.encode(jsonEncode(corpo)),
      status,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

/// Cliente da API apontando para um servidor simulado
ClienteApi apiSimulada(Future<http.Response> Function(http.Request requisicao) tratar) =>
    ClienteApi(cliente: MockClient(tratar), urlBase: 'http://api.teste');

final resumoVagasJson = {
  'total': 30,
  'disponiveis': 28,
  'ocupadas': 1,
  'manutencao': 1,
  'atualizadoEm': '2026-09-30T14:45:30Z',
};
