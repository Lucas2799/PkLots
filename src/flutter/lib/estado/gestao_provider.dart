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
      final resultados = await Future.wait([_api.dashboard(), _api.vagas(), _api.caixaAtual()]);
      dashboard = resultados[0] as Dashboard;
      vagas = resultados[1] as List<Vaga>;
      caixa = resultados[2] as Caixa?;
      erro = null;
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
    } finally {
      carregando = false;
      notifyListeners();
    }
  }

  Future<Entrada> registrarEntrada(String placa) => _eAtualizar(() => _api.registrarEntrada(placa));

  // Saída: a cobrança não altera o pátio; a confirmação registra a saída e atualiza o painel
  Future<CobrancaSaida> cobrarSaida(String placa) => _api.cobrarSaida(placa);

  Future<CobrancaSaida> cobrarSaidaPorTicket(String codigo) => _api.cobrarSaidaPorTicket(codigo);

  Future<CobrancaSaida> cobrarSaidaPorImagem(List<int> bytes, String nome) => _api.cobrarSaidaPorImagem(bytes, nome);

  Future<Saida> confirmarSaida(String cobrancaId, {String? forma, double? valorRecebido}) =>
      _eAtualizar(() => _api.confirmarSaida(cobrancaId, forma: forma, valorRecebido: valorRecebido));

  // ---------- Caixa do usuário ----------

  Caixa? caixa;

  Future<void> carregarCaixa() async {
    try {
      caixa = await _api.caixaAtual();
      notifyListeners();
    } on ExcecaoApi catch (_) {
      // o painel continua funcionando; o caixa é recarregado na próxima atualização
    }
  }

  Future<void> abrirCaixa(double troco) async {
    caixa = await _api.abrirCaixa(troco);
    notifyListeners();
  }

  Future<void> movimentarCaixa(String tipo, double valor, String? motivo) async {
    caixa = await _api.movimentarCaixa(tipo, valor, motivo);
    notifyListeners();
  }

  Future<Caixa> fecharCaixa(double contado, String? observacao) async {
    final fechado = await _api.fecharCaixa(contado, observacao);
    caixa = null;
    notifyListeners();
    return fechado;
  }

  Future<List<Caixa>> caixasDoDia() => _api.caixasDoDia();

  Future<Entrada> registrarEntradaPorImagem(List<int> bytes, String nome) =>
      _eAtualizar(() => _api.registrarEntradaPorImagem(bytes, nome));


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
