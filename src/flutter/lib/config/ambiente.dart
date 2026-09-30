import 'package:flutter/foundation.dart';

/// Endereço da API.
///
/// Pode ser definido no build: `flutter run --dart-define=API_URL=http://192.168.0.10:8000`.
/// Sem definição: na web usa a mesma origem da página (servida pelo gateway Nginx);
/// no emulador Android usa o host da máquina (10.0.2.2).
class Ambiente {
  static const _apiUrl = String.fromEnvironment('API_URL');

  static String get urlApi {
    if (_apiUrl.isNotEmpty) return _apiUrl;
    if (kIsWeb) return Uri.base.origin;
    return 'http://10.0.2.2:8000';
  }

  /// Intervalo de atualização automática das telas (o MVP ainda não tem WebSocket)
  static const intervaloAtualizacao = Duration(seconds: 15);
}
