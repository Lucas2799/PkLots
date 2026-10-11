import 'package:flutter/foundation.dart';

import '../modelos/modelos.dart';
import '../servicos/cliente_api.dart';
import '../servicos/excecao_api.dart';

class AdministracaoProvider extends ChangeNotifier {
  AdministracaoProvider(this._api);

  final ClienteApi _api;

  bool carregando = false;
  String? erro;
  ConfiguracaoPatio? configuracao;
  ConfiguracaoImpressao? impressao;
  List<UsuarioAdmin> usuarios = [];

  Future<void> carregar() async {
    carregando = true;
    notifyListeners();
    try {
      final resultados =
          await Future.wait([_api.configuracao(), _api.usuarios(), _api.configuracaoImpressao(), _api.patiosDaEmpresa()]);
      configuracao = resultados[0] as ConfiguracaoPatio;
      usuarios = resultados[1] as List<UsuarioAdmin>;
      impressao = resultados[2] as ConfiguracaoImpressao;
      patios = resultados[3] as List<PatioAdmin>;
      erro = null;
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
    } finally {
      carregando = false;
      notifyListeners();
    }
  }

  Future<void> salvarConfiguracao(String nome, double valorHora, int minutosTolerancia,
      {double? valorMensalidade, int? vagasReservadasMensalistas}) async {
    configuracao = await _api.salvarConfiguracao(nome, valorHora, minutosTolerancia,
        valorMensalidade: valorMensalidade, vagasReservadasMensalistas: vagasReservadasMensalistas);
    notifyListeners();
  }

  Future<void> alterarQuantidadeVagas(int total) async {
    configuracao = await _api.alterarQuantidadeVagas(total);
    notifyListeners();
  }

  Future<void> salvarImpressao(ConfiguracaoImpressao nova) async {
    impressao = await _api.salvarConfiguracaoImpressao(nova);
    notifyListeners();
  }

  Future<void> imprimirTesteNaRede() => _api.imprimirTeste();

  Future<void> criarUsuario(String email, String nome, String perfil, List<String> patiosIds) async {
    await _api.criarUsuario(email, nome, perfil, patiosIds);
    await _recarregarUsuarios();
  }

  Future<void> atualizarUsuario(String id, String nome, String perfil, bool ativo, List<String> patiosIds) async {
    await _api.atualizarUsuario(id, nome, perfil, ativo, patiosIds);
    await _recarregarUsuarios();
  }

  // ---------- Pátios ----------

  List<PatioAdmin> patios = [];

  Future<void> carregarPatios() async {
    patios = await _api.patiosDaEmpresa();
    notifyListeners();
  }

  Future<void> criarPatio(String nome, int totalVagas, double valorHora) async {
    await _api.criarPatio(nome, totalVagas, valorHora);
    await carregarPatios();
  }

  Future<void> atualizarPatio(String id, String nome, bool ativo) async {
    await _api.atualizarPatio(id, nome, ativo);
    await carregarPatios();
  }

  Future<void> reenviarConvite(String id) async {
    await _api.reenviarConviteUsuario(id);
    await _recarregarUsuarios();
  }

  Future<void> enviarLinkNovaSenha(String id) => _api.enviarLinkNovaSenha(id);

  Future<void> _recarregarUsuarios() async {
    usuarios = await _api.usuarios();
    notifyListeners();
  }
}
