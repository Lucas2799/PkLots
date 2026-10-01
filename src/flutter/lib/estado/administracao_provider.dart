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
      final resultados = await Future.wait([_api.configuracao(), _api.usuarios(), _api.configuracaoImpressao()]);
      configuracao = resultados[0] as ConfiguracaoPatio;
      usuarios = resultados[1] as List<UsuarioAdmin>;
      impressao = resultados[2] as ConfiguracaoImpressao;
      erro = null;
    } on ExcecaoApi catch (e) {
      erro = e.mensagem;
    } finally {
      carregando = false;
      notifyListeners();
    }
  }

  Future<void> salvarConfiguracao(String nome, double valorHora, int minutosTolerancia) async {
    configuracao = await _api.salvarConfiguracao(nome, valorHora, minutosTolerancia);
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

  Future<void> criarUsuario(String email, String nome, String senha, String perfil) async {
    await _api.criarUsuario(email, nome, senha, perfil);
    await _recarregarUsuarios();
  }

  Future<void> atualizarUsuario(String id, String nome, String perfil, bool ativo) async {
    await _api.atualizarUsuario(id, nome, perfil, ativo);
    await _recarregarUsuarios();
  }

  Future<void> redefinirSenha(String id, String senha) => _api.redefinirSenha(id, senha);

  Future<void> _recarregarUsuarios() async {
    usuarios = await _api.usuarios();
    notifyListeners();
  }
}
