/// Erro retornado pela API (ProblemDetails) ou falha de conexão
class ExcecaoApi implements Exception {
  final int? status;
  final String mensagem;

  ExcecaoApi(this.mensagem, {this.status});

  bool get naoAutorizado => status == 401;

  @override
  String toString() => mensagem;
}
