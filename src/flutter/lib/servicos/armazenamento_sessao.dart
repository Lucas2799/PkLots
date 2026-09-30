import 'package:shared_preferences/shared_preferences.dart';

/// Dados da sessão salvos no aparelho (SharedPreferences)
class DadosSessao {
  final String token;
  final String perfil;
  final DateTime expiraEm;

  DadosSessao({required this.token, required this.perfil, required this.expiraEm});

  bool get expirada => DateTime.now().isAfter(expiraEm);
}

class ArmazenamentoSessao {
  static const _chaveToken = 'pklots.token';
  static const _chavePerfil = 'pklots.perfil';
  static const _chaveExpiraEm = 'pklots.expiraEm';

  Future<void> salvar(DadosSessao sessao) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_chaveToken, sessao.token);
    await prefs.setString(_chavePerfil, sessao.perfil);
    await prefs.setString(_chaveExpiraEm, sessao.expiraEm.toIso8601String());
  }

  Future<DadosSessao?> carregar() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_chaveToken);
    final perfil = prefs.getString(_chavePerfil);
    final expiraEm = DateTime.tryParse(prefs.getString(_chaveExpiraEm) ?? '');
    if (token == null || perfil == null || expiraEm == null) return null;
    return DadosSessao(token: token, perfil: perfil, expiraEm: expiraEm);
  }

  Future<void> limpar() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_chaveToken);
    await prefs.remove(_chavePerfil);
    await prefs.remove(_chaveExpiraEm);
  }
}
