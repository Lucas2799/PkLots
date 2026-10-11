import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../tema/cores.dart';

class BarraGrafico {
  const BarraGrafico({required this.rotulo, required this.valor, required this.dica});

  /// Texto do eixo X (curto) e da primeira coluna da tabela
  final String rotulo;
  final double valor;

  /// Tooltip e célula de valor da tabela
  final String dica;
}

/// Colunas de uma série (relatórios): mesmo visual do gráfico de ocupação do painel.
/// Série única, tooltip por coluna, rótulo só no pico e alternância para tabela (acessibilidade).
class GraficoBarras extends StatefulWidget {
  const GraficoBarras({
    super.key,
    required this.titulo,
    required this.barras,
    required this.formatarEixo,
    this.mensagemVazio = 'Sem movimento no período',
  });

  final String titulo;
  final List<BarraGrafico> barras;
  final String Function(double valor) formatarEixo;
  final String mensagemVazio;

  @override
  State<GraficoBarras> createState() => _GraficoBarrasState();
}

class _GraficoBarrasState extends State<GraficoBarras> {
  bool _tabela = false;
  int? _destacado;

  static const _alturaGrafico = 160.0;
  static const _larguraMaximaColuna = 28.0;
  static const _estiloEixo = TextStyle(fontSize: 11, color: CoresPkLots.textoSuave);

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(children: [
            Expanded(child: Text(widget.titulo, style: const TextStyle(color: CoresPkLots.textoSecundario, fontSize: 13))),
            TextButton.icon(
              onPressed: () => setState(() => _tabela = !_tabela),
              icon: Icon(_tabela ? Icons.bar_chart : Icons.table_rows_outlined, size: 18),
              label: Text(_tabela ? 'Ver gráfico' : 'Ver tabela'),
            ),
          ]),
          const SizedBox(height: 8),
          if (_tabela) _construirTabela() else _construirGrafico(),
        ],
      );

  Widget _construirGrafico() {
    final barras = widget.barras;
    final maximo = barras.fold<double>(0, (m, b) => math.max(m, b.valor));
    final pico = maximo == 0 ? -1 : barras.indexWhere((b) => b.valor == maximo);
    // Rótulos do eixo X sem sobreposição: no máximo ~8
    final passo = math.max(1, (barras.length / 8).ceil());

    return Semantics(
      label: '${widget.titulo}. ${pico < 0 ? widget.mensagemVazio : 'Maior valor: ${barras[pico].dica} em ${barras[pico].rotulo}'}.',
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        SizedBox(
          height: _alturaGrafico,
          width: 56,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(widget.formatarEixo(maximo), style: _estiloEixo, softWrap: false, overflow: TextOverflow.fade),
              Text(widget.formatarEixo(maximo / 2), style: _estiloEixo, softWrap: false, overflow: TextOverflow.fade),
              const Text('0', style: _estiloEixo),
            ],
          ),
        ),
        const SizedBox(width: 6),
        Expanded(
          child: Column(children: [
            SizedBox(
              height: _alturaGrafico,
              child: Stack(children: [
                Column(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: List.generate(3, (_) => Container(height: 1, color: CoresPkLots.borda)),
                ),
                if (pico < 0)
                  Center(child: Text(widget.mensagemVazio, style: const TextStyle(color: CoresPkLots.textoSuave, fontSize: 13))),
                Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
                  for (var i = 0; i < barras.length; i++) Expanded(child: _coluna(i, barras[i], maximo, destaquePico: i == pico)),
                ]),
              ]),
            ),
            const SizedBox(height: 6),
            Row(children: [
              for (var i = 0; i < barras.length; i++)
                Expanded(
                  child: i % passo == 0
                      ? Text(barras[i].rotulo,
                          style: _estiloEixo, textAlign: TextAlign.center, softWrap: false, overflow: TextOverflow.visible)
                      : const SizedBox.shrink(),
                ),
            ]),
          ]),
        ),
      ]),
    );
  }

  Widget _coluna(int indice, BarraGrafico barra, double maximo, {required bool destaquePico}) {
    final altura = maximo == 0 ? 0.0 : (barra.valor / maximo) * (_alturaGrafico - 18);
    final destacado = _destacado == indice;
    final espaco = widget.barras.length > 40 ? 1.0 : 2.0;

    return MouseRegion(
      onEnter: (_) => setState(() => _destacado = indice),
      onExit: (_) => setState(() => _destacado = null),
      child: Tooltip(
        message: '${barra.rotulo} · ${barra.dica}',
        waitDuration: Duration.zero,
        child: Container(
          color: Colors.transparent,
          height: _alturaGrafico,
          padding: EdgeInsets.symmetric(horizontal: espaco / 2),
          alignment: Alignment.bottomCenter,
          child: Column(mainAxisAlignment: MainAxisAlignment.end, children: [
            if (destaquePico)
              Text(widget.formatarEixo(barra.valor),
                  style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                  softWrap: false,
                  overflow: TextOverflow.visible),
            const SizedBox(height: 2),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: _larguraMaximaColuna),
              child: Container(
                height: barra.valor == 0 ? 0 : math.max(altura, 2),
                decoration: BoxDecoration(
                  color: destacado ? CoresPkLots.azulMarinho : CoresPkLots.grafico,
                  borderRadius: const BorderRadius.vertical(top: Radius.circular(4)),
                ),
              ),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _construirTabela() => ConstrainedBox(
        constraints: const BoxConstraints(maxHeight: 240),
        child: SingleChildScrollView(
          child: DataTable(
            headingRowHeight: 36,
            dataRowMinHeight: 32,
            dataRowMaxHeight: 32,
            columns: const [DataColumn(label: Text('')), DataColumn(label: Text('Valor'), numeric: true)],
            rows: [
              for (final b in widget.barras) DataRow(cells: [DataCell(Text(b.rotulo)), DataCell(Text(b.dica))]),
            ],
          ),
        ),
      );
}
