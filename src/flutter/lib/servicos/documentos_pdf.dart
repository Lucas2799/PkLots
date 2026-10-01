import 'package:flutter/services.dart' show rootBundle;
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

import '../modelos/modelos.dart';
import '../util/formatadores.dart';

/// PDFs no tamanho do papel térmico (58/80 mm, altura conforme o conteúdo):
/// ticket de entrada (impressão pelo navegador) e recibo de saída (download).
class DocumentosPdf {
  static const _azul = PdfColor.fromInt(0xFF0B2545);
  static const _cinza = PdfColor.fromInt(0xFF55627A);

  static PdfPageFormat formato(int larguraMm) =>
      PdfPageFormat(larguraMm * PdfPageFormat.mm, double.infinity, marginAll: (larguraMm <= 58 ? 2 : 4) * PdfPageFormat.mm);

  static Future<pw.MemoryImage?> _icone() async {
    try {
      return pw.MemoryImage((await rootBundle.load('assets/imagens/icone.png')).buffer.asUint8List());
    } catch (_) {
      return null; // sem o ícone o documento continua válido
    }
  }

  static pw.Widget _cabecalho(pw.MemoryImage? icone, String nomePatio, String titulo, double escala) => pw.Column(children: [
        if (icone != null) pw.Image(icone, height: 26 * escala),
        pw.SizedBox(height: 2),
        pw.Text('PkLots', style: pw.TextStyle(fontSize: 14 * escala, fontWeight: pw.FontWeight.bold, color: _azul)),
        if (nomePatio.isNotEmpty) pw.Text(nomePatio, style: pw.TextStyle(fontSize: 8 * escala, color: _cinza)),
        pw.SizedBox(height: 4),
        pw.Text(titulo, style: pw.TextStyle(fontSize: 9 * escala, fontWeight: pw.FontWeight.bold)),
        pw.Divider(thickness: 0.5),
      ]);

  static pw.Widget _linha(String rotulo, String valor, double escala, {bool destaque = false}) => pw.Padding(
        padding: const pw.EdgeInsets.symmetric(vertical: 1.5),
        child: pw.Row(children: [
          pw.Expanded(child: pw.Text(rotulo, style: pw.TextStyle(fontSize: 8 * escala, color: _cinza))),
          pw.Text(valor,
              style: pw.TextStyle(
                  fontSize: (destaque ? 10 : 8) * escala, fontWeight: destaque ? pw.FontWeight.bold : pw.FontWeight.normal)),
        ]),
      );

  /// Ticket entregue na entrada (mesmo conteúdo do ticket ESC/POS da impressora de rede)
  static Future<List<int>> ticketEntrada(TicketEntrada ticket) async {
    final icone = await _icone();
    final escala = ticket.larguraPapelMm <= 58 ? 0.85 : 1.0;
    final documento = pw.Document(title: 'Ticket ${ticket.placa}', author: 'PkLots');

    documento.addPage(pw.Page(
      pageFormat: formato(ticket.larguraPapelMm),
      build: (_) => pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.stretch, children: [
        _cabecalho(icone, ticket.nomePatio, 'TICKET DE ENTRADA', escala),
        pw.Center(
          child: pw.Text(Formatadores.placa(ticket.placa),
              style: pw.TextStyle(fontSize: 22 * escala, fontWeight: pw.FontWeight.bold, color: _azul)),
        ),
        if (ticket.numeroVaga != null)
          pw.Center(
            child: pw.Text('VAGA ${ticket.numeroVaga}',
                style: pw.TextStyle(fontSize: 13 * escala, fontWeight: pw.FontWeight.bold)),
          ),
        pw.Divider(thickness: 0.5),
        _linha('Entrada', Formatadores.dataHoraCompleta(ticket.dataHoraEntrada), escala),
        _linha('Valor por hora', Formatadores.moeda(ticket.valorHora), escala),
        if (ticket.minutosTolerancia > 0) _linha('Tolerância gratuita', '${ticket.minutosTolerancia} min', escala),
        pw.Divider(thickness: 0.5),
        pw.Center(
          child: pw.BarcodeWidget(
            barcode: pw.Barcode.qrCode(),
            data: ticket.conteudoQrCode,
            width: 70 * escala,
            height: 70 * escala,
          ),
        ),
        pw.SizedBox(height: 4),
        pw.Center(
          child: pw.Text('Código: ${ticket.codigo}', style: pw.TextStyle(fontSize: 9 * escala, fontWeight: pw.FontWeight.bold)),
        ),
        pw.Center(child: pw.Text('Guarde este ticket até a saída', style: pw.TextStyle(fontSize: 7 * escala, color: _cinza))),
      ]),
    ));
    return documento.save();
  }

  /// Recibo da saída (download / compartilhamento)
  static Future<List<int>> recibo(Saida saida, {int larguraMm = 80}) async {
    final icone = await _icone();
    final escala = larguraMm <= 58 ? 0.85 : 1.0;
    final documento = pw.Document(title: 'Recibo ${saida.placa}', author: 'PkLots');

    documento.addPage(pw.Page(
      pageFormat: formato(larguraMm),
      build: (_) => pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.stretch, children: [
        _cabecalho(icone, saida.nomePatio, 'RECIBO DE ESTACIONAMENTO', escala),
        _linha('Placa', Formatadores.placa(saida.placa), escala, destaque: true),
        if (saida.numeroVaga != null) _linha('Vaga', saida.numeroVaga!, escala),
        _linha('Entrada', Formatadores.dataHoraCompleta(saida.dataHoraEntrada), escala),
        _linha('Saída', Formatadores.dataHoraCompleta(saida.dataHoraSaida), escala),
        _linha('Permanência', saida.duracaoFormatada, escala),
        _linha('Valor por hora', Formatadores.moeda(saida.valorHora), escala),
        pw.Divider(thickness: 0.5),
        _linha('TOTAL', Formatadores.moeda(saida.valorTotal), escala, destaque: true),
        if (saida.formaPagamento != null)
          _linha('Pagamento', rotulosFormaPagamento[saida.formaPagamento] ?? saida.formaPagamento!, escala),
        if (saida.valorRecebido != null) _linha('Recebido', Formatadores.moeda(saida.valorRecebido!), escala),
        if ((saida.troco ?? 0) > 0) _linha('Troco', Formatadores.moeda(saida.troco!), escala),
        if (saida.mensalistaSemCobranca)
          pw.Text('Mensalista${saida.mensalista != null ? ' ${saida.mensalista}' : ''}: sem cobrança',
              textAlign: pw.TextAlign.center, style: pw.TextStyle(fontSize: 7 * escala, color: _cinza)),
        if (saida.isento)
          pw.Text('Isento: saída dentro da tolerância gratuita',
              textAlign: pw.TextAlign.center, style: pw.TextStyle(fontSize: 7 * escala, color: _cinza)),
        pw.SizedBox(height: 6),
        pw.Center(child: pw.Text('Recibo nº ${saida.id.substring(0, 8).toUpperCase()}', style: pw.TextStyle(fontSize: 7 * escala, color: _cinza))),
        pw.Center(child: pw.Text('Obrigado pela preferência!', style: pw.TextStyle(fontSize: 8 * escala))),
      ]),
    ));
    return documento.save();
  }
}
