import 'package:intl/intl.dart';

class Formatadores {
  static final _moeda = NumberFormat.currency(locale: 'pt_BR', symbol: 'R\$');
  static final _hora = DateFormat('HH:mm', 'pt_BR');
  static final _dataHora = DateFormat("dd/MM 'às' HH:mm", 'pt_BR');
  static final _dataHoraCompleta = DateFormat('dd/MM/yyyy HH:mm', 'pt_BR');
  static final _dataCurta = DateFormat('dd/MMM', 'pt_BR');
  static final _percentual = NumberFormat.decimalPattern('pt_BR');

  static String moeda(num valor) => _moeda.format(valor);

  static final _moedaCompacta = NumberFormat.compactCurrency(locale: 'pt_BR', symbol: 'R\$', decimalDigits: 0);

  /// Eixo de gráficos: "R$ 1 mil", "R$ 250"
  static String moedaCompacta(num valor) => _moedaCompacta.format(valor);

  static String hora(DateTime data) => _hora.format(data.toLocal());

  static String dataHora(DateTime data) => _dataHora.format(data.toLocal());

  static String dataHoraCompleta(DateTime data) => _dataHoraCompleta.format(data.toLocal());

  static final _data = DateFormat('dd/MM/yyyy', 'pt_BR');

  /// Data sem hora (validade de mensalidade): não converte fuso
  static String data(DateTime data) => _data.format(data);

  static String dataCurta(DateTime data) => _dataCurta.format(data.toLocal());

  static String percentual(num valor) => '${_percentual.format(valor)}%';

  /// "ABC1234" → "ABC-1234"; placas Mercosul ficam como estão ("BRA2E19")
  static String placa(String placa) {
    final antiga = RegExp(r'^[A-Z]{3}\d{4}$');
    return antiga.hasMatch(placa) ? '${placa.substring(0, 3)}-${placa.substring(3)}' : placa;
  }

  /// Duração a partir de agora, no mesmo formato da API ("4h 15m")
  static String duracaoDesde(DateTime inicio, {DateTime? agora}) {
    final diferenca = (agora ?? DateTime.now()).difference(inicio);
    final minutos = (diferenca.inSeconds / 60).ceil().clamp(0, 1 << 31);
    return '${minutos ~/ 60}h ${minutos % 60}m';
  }
}
