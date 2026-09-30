import 'dart:async';

import 'package:flutter/foundation.dart';

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

  void iniciar() {
    carregar();
    _atualizacao?.cancel();
    _atualizacao = Timer.periodic(Ambiente.intervaloAtualizacao, (_) => carregar(silencioso: true));
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
