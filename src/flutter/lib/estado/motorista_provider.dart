import 'dart:async';

import 'package:flutter/foundation.dart';

import '../config/ambiente.dart';
import '../modelos/modelos.dart';
import '../servicos/cliente_api.dart';
import '../servicos/excecao_api.dart';

class MotoristaProvider extends ChangeNotifier {
  MotoristaProvider(this._api);

  final ClienteApi _api;
  Timer? _atualizacao;

  bool carregando = false;
  String? erro;
  /// Pátios da plataforma com vagas livres agora (null até a primeira carga)
  List<PatioDisponibilidade>? patios;
  EstacionamentoAtual? atual;
  List<Veiculo> veiculos = [];
  List<ItemHistorico> historico = [];

  /// Carrega a tela e liga a atualização automática
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
      final resultados = await Future.wait([
        _api.patiosParaMotorista(),
        _api.estacionamentoAtual(),
        _api.meusVeiculos(),
        _api.meuHistorico(),
      ]);
      patios = resultados[0] as List<PatioDisponibilidade>;
      atual = resultados[1] as EstacionamentoAtual?;
      veiculos = resultados[2] as List<Veiculo>;
      historico = resultados[3] as List<ItemHistorico>;
      erro = null;
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
    } finally {
      carregando = false;
      notifyListeners();
    }
  }

  Future<Veiculo> cadastrarVeiculo(String placa, {String? modelo, String? cor}) async {
    final veiculo = await _api.cadastrarVeiculo(placa, modelo: modelo, cor: cor);
    await carregar(silencioso: true);
    return veiculo;
  }

  Future<Saida> iniciarSaida() async {
    final saida = await _api.iniciarSaida();
    await carregar(silencioso: true);
    return saida;
  }

  @override
  void dispose() {
    _atualizacao?.cancel();
    super.dispose();
  }
}
