import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../config/ambiente.dart';
import '../modelos/modelos.dart';
import '../servicos/cliente_api.dart';
import '../servicos/excecao_api.dart';

class GestaoProvider extends ChangeNotifier {
  GestaoProvider(this._api);

  final ClienteApi _api;
  Timer? _atualizacao;

  bool carregando = false;
  String? erro;
  Dashboard? dashboard;
  List<Vaga> vagas = [];

  /// Pátios que o usuário pode usar e o escolhido no seletor (lembrado no aparelho)
  List<PatioResumo> patios = [];
  String? get patioAtualId => _api.patioAtualId;
  PatioResumo? get patioAtual => patios.where((p) => p.id == _api.patioAtualId).firstOrNull;

  static const _chavePatio = 'pklots.patio';

  void iniciar() {
    _iniciar();
    _atualizacao?.cancel();
    _atualizacao = Timer.periodic(Ambiente.intervaloAtualizacao, (_) => carregar(silencioso: true));
  }

  Future<void> _iniciar() async {
    await carregarPatios();
    await carregar();
  }

  /// Carrega os pátios permitidos e restaura a última escolha (se ainda tiver acesso)
  Future<void> carregarPatios() async {
    try {
      patios = await _api.patiosPermitidos();
      final prefs = await SharedPreferences.getInstance();
      final salvo = prefs.getString(_chavePatio);
      _api.patioAtualId = patios.any((p) => p.id == salvo) ? salvo : patios.firstOrNull?.id;
      notifyListeners();
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
      notifyListeners();
    }
  }

  Future<void> selecionarPatio(String patioId) async {
    if (patioId == _api.patioAtualId) return;
    _api.patioAtualId = patioId;
    dashboard = null;
    vagas = [];
    notifyListeners();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_chavePatio, patioId);
    await carregar();
  }


  void parar() => _atualizacao?.cancel();

  Future<void> carregar({bool silencioso = false}) async {
    if (!silencioso) {
      carregando = true;
      notifyListeners();
    }
    try {
      final resultados = await Future.wait([_api.dashboard(), _api.vagas()]);
      dashboard = resultados[0] as Dashboard;
      vagas = resultados[1] as List<Vaga>;
      erro = null;
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
    } finally {
      carregando = false;
      notifyListeners();
    }
  }

  Future<Entrada> registrarEntrada(String placa) => _eAtualizar(() => _api.registrarEntrada(placa));

  Future<Saida> registrarSaida(String placa) => _eAtualizar(() => _api.registrarSaida(placa));

  Future<Saida> registrarSaidaPorTicket(String codigo) => _eAtualizar(() => _api.registrarSaidaPorTicket(codigo));

  Future<Entrada> registrarEntradaPorImagem(List<int> bytes, String nome) =>
      _eAtualizar(() => _api.registrarEntradaPorImagem(bytes, nome));

  Future<Saida> registrarSaidaPorImagem(List<int> bytes, String nome) =>
      _eAtualizar(() => _api.registrarSaidaPorImagem(bytes, nome));

  Future<Vaga> marcarManutencao(String vagaId, String? observacoes) =>
      _eAtualizar(() => _api.marcarManutencao(vagaId, observacoes));

  Future<Vaga> liberarManutencao(String vagaId) => _eAtualizar(() => _api.liberarManutencao(vagaId));

  Future<List<ItemHistorico>> historicoVeiculo(String placa) => _api.historicoVeiculo(placa);

  Future<T> _eAtualizar<T>(Future<T> Function() acao) async {
    final resultado = await acao();
    await carregar(silencioso: true);
    return resultado;
  }

  @override
  void dispose() {
    _atualizacao?.cancel();
    super.dispose();
  }
}
