/// Validações de formulário (mesmas regras da API)
class Validadores {
  static final _placa = RegExp(r'^[A-Z]{3}\d[A-Z0-9]\d{2}$');

  /// Remove espaços e hífen e deixa em maiúsculas: "abc-1234" → "ABC1234"
  static String normalizarPlaca(String texto) => texto.replaceAll(RegExp(r'[\s\-]'), '').toUpperCase();

  static String? placa(String? texto) {
    if (texto == null || texto.trim().isEmpty) return 'Informe a placa';
    return _placa.hasMatch(normalizarPlaca(texto)) ? null : 'Placa inválida (ex.: ABC1234 ou ABC1D23)';
  }

  static String? email(String? texto) {
    if (texto == null || texto.trim().isEmpty) return 'Informe o e-mail';
    return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(texto.trim()) ? null : 'E-mail inválido';
  }

  static String? senha(String? texto) {
    if (texto == null || texto.isEmpty) return 'Informe a senha';
    return texto.length < 8 ? 'A senha deve ter pelo menos 8 caracteres' : null;
  }

  /// Senha nova: mesma política da API (8+ caracteres, letras e números)
  static String? novaSenha(String? senha) {
    if (senha == null || senha.length < 8) return 'Mínimo de 8 caracteres';
    if (!senha.contains(RegExp(r'[A-Za-z]')) || !senha.contains(RegExp(r'\d'))) return 'Use letras e números';
    return null;
  }

  static String? obrigatorio(String? texto, String campo) =>
      texto == null || texto.trim().isEmpty ? 'Informe $campo' : null;
}
