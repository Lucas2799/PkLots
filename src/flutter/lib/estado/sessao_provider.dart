import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../modelos/modelos.dart';
import '../servicos/armazenamento_sessao.dart';
import '../servicos/cliente_api.dart';

enum SituacaoSessao { carregando, deslogado, logado }

class SessaoProvider extends ChangeNotifier {
  SessaoProvider(this._api, this._armazenamento) {
    _api.aoExpirarSessao = sair;
  }

  final ClienteApi _api;
  final ArmazenamentoSessao _armazenamento;

  SituacaoSessao situacao = SituacaoSessao.carregando;
  String? perfil;
  String? nomeUsuario;
  String? emailUsuario;

  bool get ehGestao => perfil == 'admin' || perfil == 'operador';

  bool get ehAdmin => perfil == 'admin';

  bool get ehSuperAdmin => perfil == 'superadmin';

  /// Primeiro nome, para a saudação no topo
  String get primeiroNome => (nomeUsuario ?? '').split(' ').first;

  Future<void> restaurar() async {
    final sessao = await _armazenamento.carregar();
    if (sessao == null || sessao.expirada) {
      await _armazenamento.limpar();
      _definirDeslogado();
      return;
    }
    _aplicar(sessao.token, sessao.perfil);
  }

  Future<void> entrar(String email, String senha) async {
    await _iniciar(await _api.login(email.trim(), senha));
  }

  Future<void> registrar(String nome, String email, String senha) async {
    await _iniciar(await _api.registrar(email.trim(), senha, nome.trim()));
  }

  Future<void> sair() async {
    await _armazenamento.limpar();
    _definirDeslogado();
  }

  Future<void> _iniciar(Token token) async {
    final expiraEm = DateTime.now().add(Duration(seconds: token.expiraEm));
    await _armazenamento.salvar(DadosSessao(token: token.tokenAcesso, perfil: token.perfil, expiraEm: expiraEm));
    _aplicar(token.tokenAcesso, token.perfil);
  }

  void _aplicar(String token, String perfilUsuario) {
    _api.token = token;
    perfil = perfilUsuario;
    final dados = lerDadosToken(token);
    nomeUsuario = dados['name'] as String?;
    emailUsuario = dados['email'] as String?;
    situacao = SituacaoSessao.logado;
    notifyListeners();
  }

  void _definirDeslogado() {
    _api.token = null;
    _api.patioAtualId = null;
    perfil = null;
    nomeUsuario = null;
    emailUsuario = null;
    situacao = SituacaoSessao.deslogado;
    notifyListeners();
  }

  /// Lê o payload do JWT (sem validar: a validação é feita pela API)
  @visibleForTesting
  static Map<String, dynamic> lerDadosToken(String token) {
    try {
      final partes = token.split('.');
      if (partes.length != 3) return {};
      final payload = utf8.decode(base64Url.decode(base64Url.normalize(partes[1])));
      return jsonDecode(payload) as Map<String, dynamic>;
    } catch (_) {
      return {};
    }
  }
}
