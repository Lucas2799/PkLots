import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';
import '../util/formatadores.dart';

/// Colunas de vagas ocupadas por hora nas últimas 24h.
/// Série única: sem legenda (o título a nomeia); tooltip por coluna; rótulo só no pico;
/// alternância para tabela (acessibilidade).
class GraficoOcupacao extends StatefulWidget {
  const GraficoOcupacao({super.key, required this.pontos, required this.totalVagas});

  final List<OcupacaoHora> pontos;
  final int totalVagas;

  @override
  State<GraficoOcupacao> createState() => _GraficoOcupacaoState();
}

class _GraficoOcupacaoState extends State<GraficoOcupacao> {
  bool _tabela = false;
  int? _destacado;

  static const _alturaGrafico = 160.0;
  static const _larguraMaximaColuna = 24.0;
  static const _espaco = 2.0;
  static const _estiloEixo = TextStyle(fontSize: 11, color: CoresPkLots.textoSuave);

  @override
  Widget build(BuildContext context) {
    final pontos = widget.pontos;
    final maximo = math.max(widget.totalVagas, pontos.fold<int>(1, (m, p) => math.max(m, p.ocupadas)));
    final pico = pontos.isEmpty ? null : pontos.reduce((a, b) => b.ocupadas >= a.ocupadas ? b : a);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(children: [
          const Expanded(
            child: Text('Vagas ocupadas por hora (últimas 24h)',
                style: TextStyle(color: CoresPkLots.textoSecundario, fontSize: 13)),
          ),
          TextButton.icon(
            onPressed: () => setState(() => _tabela = !_tabela),
            icon: Icon(_tabela ? Icons.bar_chart : Icons.table_rows_outlined, size: 18),
            label: Text(_tabela ? 'Ver gráfico' : 'Ver tabela'),
          ),
        ]),
        const SizedBox(height: 8),
        if (_tabela) _construirTabela(pontos) else _construirGrafico(pontos, maximo, pico),
      ],
    );
  }

  Widget _construirGrafico(List<OcupacaoHora> pontos, int maximo, OcupacaoHora? pico) {
    final resumo = pico == null
        ? 'Sem dados de ocupação'
        : 'Pico de ${pico.ocupadas} vagas ocupadas às ${Formatadores.hora(pico.hora)}';

    return Semantics(
      label: 'Gráfico de ocupação das últimas 24 horas. $resumo.',
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Eixo Y: apenas 0, metade e total, discretos
          SizedBox(
            height: _alturaGrafico,
            width: 28,
            child: Column(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text('$maximo', style: _estiloEixo),
                Text('${maximo ~/ 2}', style: _estiloEixo),
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
                  // Linhas de grade recessivas
                  Column(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: List.generate(3, (i) => Container(height: 1, color: CoresPkLots.borda)),
                  ),
                  if (pico == null || pico.ocupadas == 0)
                    const Center(
                      child: Text('Nenhuma vaga ocupada nas últimas 24h',
                          style: TextStyle(color: CoresPkLots.textoSuave, fontSize: 13)),
                    ),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      for (var i = 0; i < pontos.length; i++)
                        Expanded(child: _coluna(i, pontos[i], maximo, destaquePico: identical(pontos[i], pico))),
                    ],
                  ),
                ]),
              ),
              const SizedBox(height: 6),
              // Eixo X: rótulo a cada 4 horas
              Row(children: [
                for (var i = 0; i < pontos.length; i++)
                  Expanded(
                    child: i % 4 == 3
                        ? Text(i == pontos.length - 1 ? 'agora' : Formatadores.hora(pontos[i].hora),
                            style: _estiloEixo, textAlign: TextAlign.center, softWrap: false, overflow: TextOverflow.visible)
                        : const SizedBox.shrink(),
                  ),
              ]),
            ]),
          ),
        ],
      ),
    );
  }

  Widget _coluna(int indice, OcupacaoHora ponto, int maximo, {required bool destaquePico}) {
    final altura = maximo == 0 ? 0.0 : (ponto.ocupadas / maximo) * (_alturaGrafico - 18);
    final destacado = _destacado == indice;

    return MouseRegion(
      onEnter: (_) => setState(() => _destacado = indice),
      onExit: (_) => setState(() => _destacado = null),
      child: Tooltip(
        message: '${indice == widget.pontos.length - 1 ? 'Agora' : Formatadores.hora(ponto.hora)} · ${ponto.ocupadas} ocupadas '
            '(${Formatadores.percentual(ponto.percentual)})',
        waitDuration: Duration.zero,
        // Área de toque ocupa toda a altura da faixa, maior que a coluna
        child: Container(
          color: Colors.transparent,
          height: _alturaGrafico,
          padding: const EdgeInsets.symmetric(horizontal: _espaco / 2),
          alignment: Alignment.bottomCenter,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              if (destaquePico && ponto.ocupadas > 0)
                Text('${ponto.ocupadas}',
                    style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
              const SizedBox(height: 2),
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: _larguraMaximaColuna),
                child: Container(
                  height: ponto.ocupadas == 0 ? 0 : math.max(altura, 2),
                  decoration: BoxDecoration(
                    color: destacado ? CoresPkLots.azulMarinho : CoresPkLots.grafico,
                    borderRadius: const BorderRadius.vertical(top: Radius.circular(4)),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _construirTabela(List<OcupacaoHora> pontos) => ConstrainedBox(
        constraints: const BoxConstraints(maxHeight: 240),
        child: SingleChildScrollView(
          child: DataTable(
            headingRowHeight: 36,
            dataRowMinHeight: 32,
            dataRowMaxHeight: 32,
            columns: const [
              DataColumn(label: Text('Hora')),
              DataColumn(label: Text('Ocupadas'), numeric: true),
              DataColumn(label: Text('Ocupação'), numeric: true),
            ],
            rows: [
              for (final p in pontos.reversed)
                DataRow(cells: [
                  DataCell(Text(identical(p, pontos.last) ? 'Agora' : Formatadores.hora(p.hora))),
                  DataCell(Text('${p.ocupadas}')),
                  DataCell(Text(Formatadores.percentual(p.percentual))),
                ]),
            ],
          ),
        ),
      );
}
