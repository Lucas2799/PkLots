import 'package:flutter/services.dart' show Uint8List, rootBundle;
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import '../modelos/modelos.dart';
import '../util/formatadores.dart';

/// Relatórios em PDF A4 (download), com as mesmas seções da tela
class RelatoriosPdf {
  static const _azul = PdfColor.fromInt(0xFF0B2545);
  static const _cinza = PdfColor.fromInt(0xFF55627A);
  static const _borda = PdfColor.fromInt(0xFFE2E6EC);
  static const _fundoCabecalho = PdfColor.fromInt(0xFFF6F7F9);

  static final _dia = DateFormat('dd/MM/yyyy', 'pt_BR');
  static final _diaCurto = DateFormat('dd/MM', 'pt_BR');

  static String _periodo(PeriodoRelatorio p) =>
      p.dias == 1 ? _dia.format(p.de) : '${_dia.format(p.de)} a ${_dia.format(p.ate)}';

  static Future<pw.MemoryImage?> _icone() async {
    try {
      return pw.MemoryImage((await rootBundle.load('assets/imagens/icone.png')).buffer.asUint8List());
    } catch (_) {
      return null;
    }
  }

  static pw.Widget _cabecalho(pw.MemoryImage? icone, String titulo, String subtitulo, PeriodoRelatorio periodo) => pw.Column(
        crossAxisAlignment: pw.CrossAxisAlignment.start,
        children: [
          pw.Row(children: [
            if (icone != null) ...[pw.Image(icone, height: 28), pw.SizedBox(width: 8)],
            pw.Text('PkLots', style: pw.TextStyle(fontSize: 16, fontWeight: pw.FontWeight.bold, color: _azul)),
            pw.Spacer(),
            pw.Text('Gerado em ${Formatadores.dataHoraCompleta(DateTime.now())}', style: const pw.TextStyle(fontSize: 8, color: _cinza)),
          ]),
          pw.SizedBox(height: 10),
          pw.Text(titulo, style: pw.TextStyle(fontSize: 14, fontWeight: pw.FontWeight.bold, color: _azul)),
          pw.Text('$subtitulo · ${_periodo(periodo)}', style: const pw.TextStyle(fontSize: 10, color: _cinza)),
          pw.Divider(color: _borda),
        ],
      );

  static pw.Widget _titulo(String texto) => pw.Padding(
        padding: const pw.EdgeInsets.only(top: 14, bottom: 6),
        child: pw.Text(texto, style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: _azul)),
      );

  /// Indicadores em grade de 4 colunas
  static pw.Widget _indicadores(List<(String, String)> itens) => pw.Wrap(
        spacing: 6,
        runSpacing: 6,
        children: [
          for (final (rotulo, valor) in itens)
            pw.Container(
              width: 125,
              padding: const pw.EdgeInsets.all(6),
              decoration: pw.BoxDecoration(border: pw.Border.all(color: _borda), borderRadius: pw.BorderRadius.circular(4)),
              child: pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.start, children: [
                pw.Text(rotulo, style: const pw.TextStyle(fontSize: 8, color: _cinza)),
                pw.SizedBox(height: 2),
                pw.Text(valor, style: pw.TextStyle(fontSize: 11, fontWeight: pw.FontWeight.bold, color: _azul)),
              ]),
            ),
        ],
      );

  static pw.Widget _tabela(List<String> colunas, List<List<String>> linhas, {Set<int> numericas = const {}}) => linhas.isEmpty
      ? pw.Text('Nada no período.', style: const pw.TextStyle(fontSize: 9, color: _cinza))
      : pw.TableHelper.fromTextArray(
          headers: colunas,
          data: linhas,
          headerStyle: pw.TextStyle(fontSize: 8, fontWeight: pw.FontWeight.bold, color: _azul),
          headerDecoration: const pw.BoxDecoration(color: _fundoCabecalho),
          cellStyle: const pw.TextStyle(fontSize: 8),
          cellPadding: const pw.EdgeInsets.symmetric(horizontal: 4, vertical: 3),
          border: const pw.TableBorder(horizontalInside: pw.BorderSide(color: _borda, width: 0.5)),
          cellAlignments: {for (final i in numericas) i: pw.Alignment.centerRight},
          headerAlignments: {for (final i in numericas) i: pw.Alignment.centerRight},
        );

  static pw.Widget _rodape(pw.Context contexto) => pw.Align(
        alignment: pw.Alignment.centerRight,
        child: pw.Text('Página ${contexto.pageNumber} de ${contexto.pagesCount}', style: const pw.TextStyle(fontSize: 8, color: _cinza)),
      );

  static String _nomeArquivo(String texto) =>
      texto.toLowerCase().replaceAll(RegExp(r'[^a-z0-9]+'), '-').replaceAll(RegExp(r'^-|-$'), '');

  static Future<void> baixarEmpresa(RelatorioEmpresa r) async => Printing.sharePdf(
      bytes: await gerarEmpresa(r), filename: 'relatorio-${_nomeArquivo(r.nomeEmpresa)}-${r.periodo.sufixo}.pdf');

  static Future<Uint8List> gerarEmpresa(RelatorioEmpresa r) async {
    final icone = await _icone();
    final t = r.totais;
    final documento = pw.Document(title: 'Relatório ${r.nomeEmpresa}', author: 'PkLots');

    documento.addPage(pw.MultiPage(
      pageFormat: PdfPageFormat.a4,
      margin: const pw.EdgeInsets.all(32),
      footer: _rodape,
      build: (_) => [
        _cabecalho(icone, 'Relatório gerencial', '${r.nomeEmpresa} · ${r.nomePatio ?? 'Todos os pátios'}', r.periodo),
        _indicadores([
          ('Receita total', Formatadores.moeda(t.receitaTotal)),
          ('Avulsos', Formatadores.moeda(t.receitaAvulsos)),
          ('Mensalidades', Formatadores.moeda(t.receitaMensalidades)),
          ('Ticket médio', Formatadores.moeda(t.ticketMedio)),
          ('Entradas', '${t.entradas}'),
          ('Saídas', '${t.saidas}'),
          ('Permanência média', t.permanenciaMedia),
          ('Cancelamentos', '${t.cancelamentos}'),
        ]),
        _titulo('Recebimentos por forma de pagamento'),
        _tabela(['Forma', 'Quantidade', 'Total'], [
          for (final f in r.porForma) [rotulosFormaPagamento[f.forma] ?? 'Não informada', '${f.quantidade}', Formatadores.moeda(f.total)],
        ], numericas: {1, 2}),
        if (r.porPatio.length > 1) ...[
          _titulo('Por pátio'),
          _tabela(['Pátio', 'Vagas', 'Entradas', 'Saídas', 'Giro/vaga/dia', 'Avulsos', 'Mensalidades', 'Total'], [
            for (final p in r.porPatio)
              [
                p.ativo ? p.nome : '${p.nome} (inativo)',
                '${p.vagas}',
                '${p.entradas}',
                '${p.saidas}',
                p.giroPorVaga.toStringAsFixed(2).replaceAll('.', ','),
                Formatadores.moeda(p.receitaAvulsos),
                Formatadores.moeda(p.receitaMensalidades),
                Formatadores.moeda(p.receita),
              ],
          ], numericas: {1, 2, 3, 4, 5, 6, 7}),
        ],
        _titulo('Operadores'),
        _tabela(['Nome', 'Saídas cobradas', 'Mensalidades', 'Recebido', 'Caixas fechados', 'Diferença de caixa', 'Cancelamentos'], [
          for (final o in r.porOperador)
            [
              o.nome,
              '${o.saidas}',
              '${o.mensalidades}',
              Formatadores.moeda(o.recebido),
              '${o.caixasFechados}',
              Formatadores.moeda(o.diferencaCaixa),
              '${o.cancelamentos}',
            ],
        ], numericas: {1, 2, 3, 4, 5, 6}),
        _titulo('Mensalistas'),
        _indicadores([
          ('Ativos hoje', '${r.mensalistas.ativos}'),
          ('Em dia', '${r.mensalistas.emDia}'),
          ('Vencidos', '${r.mensalistas.vencidos}'),
          ('Novos no período', '${r.mensalistas.novos}'),
        ]),
        _titulo('Movimento por dia'),
        _tabela(['Dia', 'Entradas', 'Saídas', 'Avulsos', 'Mensalidades', 'Total'], [
          for (final d in r.porDia)
            [
              _diaCurto.format(d.data),
              '${d.entradas}',
              '${d.saidas}',
              Formatadores.moeda(d.receitaAvulsos),
              Formatadores.moeda(d.receitaMensalidades),
              Formatadores.moeda(d.receita),
            ],
        ], numericas: {1, 2, 3, 4, 5}),
        _titulo('Entradas canceladas'),
        _tabela(['Quando', 'Placa', 'Pátio', 'Cancelada por', 'Motivo'], [
          for (final c in r.cancelamentos)
            [Formatadores.dataHoraCompleta(c.canceladaEm), Formatadores.placa(c.placa), c.nomePatio, c.canceladaPor ?? '-', c.motivo ?? '-'],
        ]),
      ],
    ));

    return documento.save();
  }
}
