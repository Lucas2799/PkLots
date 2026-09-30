import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:pklots/util/formatadores.dart';
import 'package:pklots/util/validadores.dart';

void main() {
  setUpAll(() => initializeDateFormatting('pt_BR'));

  group('Formatadores', () {
    test('moeda em reais', () {
      expect(Formatadores.moeda(63.75), 'R\$ 63,75');
      expect(Formatadores.moeda(2847.5), 'R\$ 2.847,50');
    });

    test('placa antiga ganha hífen; Mercosul fica igual', () {
      expect(Formatadores.placa('ABC1234'), 'ABC-1234');
      expect(Formatadores.placa('BRA2E19'), 'BRA2E19');
    });

    test('duração arredonda para cima ao minuto, como a API', () {
      final inicio = DateTime(2026, 9, 30, 10, 30, 45);
      expect(Formatadores.duracaoDesde(inicio, agora: DateTime(2026, 9, 30, 14, 45, 30)), '4h 15m');
      expect(Formatadores.duracaoDesde(inicio, agora: inicio), '0h 0m');
    });

    test('percentual com vírgula', () => expect(Formatadores.percentual(93.3), '93,3%'));
  });

  group('Validadores', () {
    test('normaliza placa', () => expect(Validadores.normalizarPlaca(' abc-1234 '), 'ABC1234'));

    test('aceita placas brasileiras', () {
      for (final placa in ['ABC1234', 'abc-1234', 'BRA2E19', 'bra 2e19']) {
        expect(Validadores.placa(placa), isNull, reason: placa);
      }
    });

    test('rejeita placas inválidas', () {
      for (final placa in ['', 'AB1234', 'ABCD123', 'ABC12345', 'ABC1D2E']) {
        expect(Validadores.placa(placa), isNotNull, reason: placa);
      }
    });

    test('e-mail e senha', () {
      expect(Validadores.email('driver@example.com'), isNull);
      expect(Validadores.email('driver@'), isNotNull);
      expect(Validadores.senha('1234567'), isNotNull);
      expect(Validadores.senha('12345678'), isNull);
    });
  });
}
