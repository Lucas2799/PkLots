import 'dart:typed_data';

import 'package:printing/printing.dart';

import '../modelos/modelos.dart';
import '../util/formatadores.dart';
import 'cliente_api.dart';
import 'documentos_pdf.dart';
import 'excecao_api.dart';

/// Impressão do ticket de entrada e download do recibo
class ImpressaoTickets {
  ImpressaoTickets(this._api);

  final ClienteApi _api;

  /// Abre o diálogo de impressão do navegador/sistema com o ticket no tamanho do papel
  Future<void> imprimirPeloNavegador(String entradaId) async {
    final ticket = await _api.ticketEntrada(entradaId);
    final bytes = Uint8List.fromList(await DocumentosPdf.ticketEntrada(ticket));
    await Printing.layoutPdf(
      name: 'ticket-${ticket.placa}',
      format: DocumentosPdf.formato(ticket.larguraPapelMm),
      onLayout: (_) async => bytes,
    );
  }

  /// Reimpressão: usa a impressora de rede; sem impressora de rede (409), imprime pelo navegador.
  /// Retorna true quando foi para a impressora de rede.
  Future<bool> reimprimir(String entradaId) async {
    try {
      await _api.imprimirTicketEntrada(entradaId);
      return true;
    } on ExcecaoApi catch (e) {
      if (e.status != 409) rethrow;
      await imprimirPeloNavegador(entradaId);
      return false;
    }
  }

  /// Baixa (web) ou compartilha (celular) o recibo em PDF
  static Future<void> baixarRecibo(Saida saida) async {
    final bytes = Uint8List.fromList(await DocumentosPdf.recibo(saida));
    final data = Formatadores.dataHoraCompleta(saida.dataHoraSaida).replaceAll(RegExp(r'[/: ]'), '-');
    await Printing.sharePdf(bytes: bytes, filename: 'recibo-${saida.placa}-$data.pdf');
  }
}
