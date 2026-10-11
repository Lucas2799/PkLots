import 'dart:async';

import 'package:flutter/foundation.dart';

import '../config/ambiente.dart';
import '../modelos/modelos.dart';
import '../servicos/cliente_api.dart';
import '../servicos/excecao_api.dart';

/// App do mensalista: contratos (com os veículos estacionados agora), histórico e vagas dos pátios
class MotoristaProvider extends ChangeNotifier {
  MotoristaProvider(this._api);

  final ClienteApi _api;
  Timer? _atualizacao;

  bool carregando = false;
  String? erro;

  /// Pátios da plataforma com vagas livres agora (null até a primeira carga)
  List<PatioDisponibilidade>? patios;
  List<ContratoMensalista> contratos = [];
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
      final resultados = await Future.wait([_api.patiosParaMotorista(), _api.meusContratos(), _api.meuHistorico()]);
      patios = resultados[0] as List<PatioDisponibilidade>;
      contratos = resultados[1] as List<ContratoMensalista>;
      historico = resultados[2] as List<ItemHistorico>;
      erro = null;
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
    } finally {
      carregando = false;
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _atualizacao?.cancel();
    super.dispose();
  }
}
