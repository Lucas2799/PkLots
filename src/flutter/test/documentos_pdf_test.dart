import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:pklots/modelos/modelos.dart';
import 'package:pklots/servicos/documentos_pdf.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() => initializeDateFormatting('pt_BR'));

  TicketEntrada ticket(int largura) => TicketEntrada(
        entradaId: 'e1',
        codigo: '1A2B3C4D',
        nomePatio: 'Pátio Padrão',
        placa: 'ABC1234',
        numeroVaga: 'A1',
        dataHoraEntrada: DateTime.utc(2026, 9, 30, 13, 30),
        valorHora: 15,
        minutosTolerancia: 15,
        larguraPapelMm: largura,
        conteudoQrCode: 'PKLOTS:e1',
      );

  bool ehPdf(List<int> bytes) => ascii.decode(bytes.take(5).toList()) == '%PDF-';

  for (final largura in [58, 80]) {
    test('gera o ticket de entrada em PDF para papel de $largura mm', () async {
      final bytes = await DocumentosPdf.ticketEntrada(ticket(largura));
      expect(ehPdf(bytes), isTrue);
      expect(bytes.length, greaterThan(1000));
    });
  }

  test('ticket com tabela de preço por faixas', () async {
    final base = ticket(58);
    final comTabela = TicketEntrada(
      entradaId: base.entradaId,
      codigo: base.codigo,
      nomePatio: base.nomePatio,
      placa: base.placa,
      dataHoraEntrada: base.dataHoraEntrada,
      valorHora: base.valorHora,
      minutosTolerancia: base.minutosTolerancia,
      larguraPapelMm: 58,
      conteudoQrCode: base.conteudoQrCode,
      descricaoPreco: '1ª hora R\$ 10,00 · cada 30 min adicionais ou fração R\$ 3,00 · diária máx. R\$ 50,00',
    );
    expect(ehPdf(await DocumentosPdf.ticketEntrada(comTabela)), isTrue);
  });

  test('gera o recibo de saída em PDF (inclusive isento)', () async {
    final saida = Saida(
      id: '0f1e2d3c-0000',
      placa: 'EDM5E01',
      dataHoraEntrada: DateTime.utc(2026, 9, 30, 13, 30),
      dataHoraSaida: DateTime.utc(2026, 9, 30, 13, 38),
      duracaoFormatada: '0h 8m',
      valorHora: 15,
      valorTotal: 0,
      situacaoPagamento: 'isento',
      numeroVaga: 'A1',
      nomePatio: 'Pátio Padrão',
    );

    final bytes = await DocumentosPdf.recibo(saida);
    expect(ehPdf(bytes), isTrue);
  });

  test('formato tem a largura do papel e altura conforme o conteúdo', () {
    expect(DocumentosPdf.formato(80).width, closeTo(80 * 72 / 25.4, 0.01));
    expect(DocumentosPdf.formato(58).width, closeTo(58 * 72 / 25.4, 0.01));
    expect(DocumentosPdf.formato(80).height, double.infinity);
  });
}
