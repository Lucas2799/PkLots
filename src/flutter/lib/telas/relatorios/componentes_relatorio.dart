import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../modelos/modelos.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/grafico_barras.dart';

/// Atalhos de período usados nos relatórios
enum AtalhoPeriodo {
  hoje('Hoje'),
  seteDias('7 dias'),
  trintaDias('30 dias'),
  esteMes('Este mês'),
  mesPassado('Mês passado'),
  personalizado('Personalizado');

  const AtalhoPeriodo(this.rotulo);
  final String rotulo;

  PeriodoRelatorio? periodo(DateTime dia) => switch (this) {
        AtalhoPeriodo.hoje => PeriodoRelatorio(dia, dia),
        seteDias => PeriodoRelatorio(DateTime(dia.year, dia.month, dia.day - 6), dia),
        trintaDias => PeriodoRelatorio(DateTime(dia.year, dia.month, dia.day - 29), dia),
        esteMes => PeriodoRelatorio(DateTime(dia.year, dia.month), dia),
        mesPassado => PeriodoRelatorio(DateTime(dia.year, dia.month - 1), DateTime(dia.year, dia.month, 0)),
        personalizado => null,
      };
}

final _dataPeriodo = DateFormat('dd/MM/yyyy', 'pt_BR');

String descreverPeriodo(PeriodoRelatorio p) =>
    p.dias == 1 ? _dataPeriodo.format(p.de) : '${_dataPeriodo.format(p.de)} a ${_dataPeriodo.format(p.ate)}';

/// Chips de período; "Personalizado" abre o calendário (até 366 dias, como a API)
class SeletorPeriodo extends StatelessWidget {
  const SeletorPeriodo({super.key, required this.atalho, required this.periodo, required this.aoMudar});

  final AtalhoPeriodo atalho;
  final PeriodoRelatorio periodo;
  final void Function(AtalhoPeriodo atalho, PeriodoRelatorio periodo) aoMudar;

  Future<void> _escolher(BuildContext context, AtalhoPeriodo escolhido) async {
    final agora = DateTime.now();
    final hoje = DateTime(agora.year, agora.month, agora.day);
    if (escolhido.periodo(hoje) case final p?) return aoMudar(escolhido, p);

    final intervalo = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2024),
      lastDate: hoje,
      initialDateRange: DateTimeRange(start: periodo.de, end: periodo.ate),
      helpText: 'Período do relatório',
      saveText: 'Aplicar',
    );
    if (intervalo == null) return;
    if (intervalo.duration.inDays + 1 > 366) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Escolha um período de até 366 dias.')));
      }
      return;
    }
    aoMudar(escolhido, PeriodoRelatorio(intervalo.start, intervalo.end));
  }

  @override
  Widget build(BuildContext context) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Wrap(spacing: 8, runSpacing: 8, children: [
          for (final a in AtalhoPeriodo.values)
            ChoiceChip(
              label: Text(a.rotulo),
              avatar: a == AtalhoPeriodo.personalizado ? const Icon(Icons.date_range, size: 18) : null,
              selected: a == atalho,
              onSelected: (_) => _escolher(context, a),
            ),
        ]),
        const SizedBox(height: 8),
        Text(descreverPeriodo(periodo), style: const TextStyle(color: CoresPkLots.textoSecundario, fontSize: 13)),
      ]);
}

/// Número de destaque (receita, entradas...) em grade responsiva
class CartaoIndicador extends StatelessWidget {
  const CartaoIndicador({super.key, required this.rotulo, required this.valor, required this.icone, this.detalhe, this.alerta = false});

  final String rotulo;
  final String valor;
  final IconData icone;
  final String? detalhe;
  final bool alerta;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Icon(icone, size: 18, color: alerta ? CoresPkLots.manutencao : CoresPkLots.textoSecundario),
              const SizedBox(width: 6),
              Expanded(
                child: Text(rotulo,
                    style: const TextStyle(fontSize: 13, color: CoresPkLots.textoSecundario), overflow: TextOverflow.ellipsis),
              ),
            ]),
            const SizedBox(height: 6),
            Text(valor,
                style: TextStyle(
                    fontSize: 20, fontWeight: FontWeight.w700, color: alerta ? CoresPkLots.manutencao : CoresPkLots.azulMarinho)),
            if (detalhe != null) ...[
              const SizedBox(height: 2),
              Text(detalhe!, style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
            ],
          ]),
        ),
      );
}

/// Grade de indicadores: 2 colunas no celular, até 4 no computador
class GradeIndicadores extends StatelessWidget {
  const GradeIndicadores({super.key, required this.itens});

  final List<Widget> itens;

  @override
  Widget build(BuildContext context) => LayoutBuilder(builder: (context, restricoes) {
        final colunas = restricoes.maxWidth >= 720 ? 4 : 2;
        final largura = (restricoes.maxWidth - (colunas - 1) * 8) / colunas;
        return Wrap(spacing: 8, runSpacing: 8, children: [
          for (final item in itens) SizedBox(width: largura, child: item),
        ]);
      });
}

/// Cartão com título e conteúdo (seções do relatório)
class SecaoRelatorio extends StatelessWidget {
  const SecaoRelatorio({super.key, required this.titulo, required this.icone, required this.filho, this.subtitulo});

  final String titulo;
  final IconData icone;
  final String? subtitulo;
  final Widget filho;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Icon(icone, size: 20, color: CoresPkLots.azulMarinho),
              const SizedBox(width: 8),
              Expanded(
                child: Text(titulo, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
              ),
            ]),
            if (subtitulo != null) ...[
              const SizedBox(height: 4),
              Text(subtitulo!, style: const TextStyle(fontSize: 13, color: CoresPkLots.textoSecundario)),
            ],
            const SizedBox(height: 12),
            filho,
          ]),
        ),
      );
}

/// Tabela com rolagem horizontal no celular
class TabelaRelatorio extends StatelessWidget {
  const TabelaRelatorio({super.key, required this.colunas, required this.linhas, this.numericas = const {}});

  final List<String> colunas;
  final List<List<String>> linhas;

  /// Índices das colunas alinhadas à direita
  final Set<int> numericas;

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          headingRowHeight: 36,
          dataRowMinHeight: 36,
          dataRowMaxHeight: 48,
          horizontalMargin: 8,
          columnSpacing: 20,
          columns: [
            for (final (i, c) in colunas.indexed) DataColumn(label: Text(c), numeric: numericas.contains(i)),
          ],
          rows: [
            for (final linha in linhas) DataRow(cells: [for (final c in linha) DataCell(Text(c))]),
          ],
        ),
      );
}

/// Receita por dia (avulsos + mensalidades)
GraficoBarras graficoReceitaPorDia(List<MovimentoDia> dias, {String titulo = 'Receita por dia'}) => GraficoBarras(
      titulo: titulo,
      formatarEixo: Formatadores.moedaCompacta,
      barras: [
        for (final d in dias)
          BarraGrafico(rotulo: DateFormat('dd/MM', 'pt_BR').format(d.data), valor: d.receita, dica: Formatadores.moeda(d.receita)),
      ],
    );

/// Entradas por dia
GraficoBarras graficoEntradasPorDia(List<MovimentoDia> dias, {String titulo = 'Entradas por dia'}) => GraficoBarras(
      titulo: titulo,
      formatarEixo: (v) => v.round().toString(),
      barras: [
        for (final d in dias)
          BarraGrafico(rotulo: DateFormat('dd/MM', 'pt_BR').format(d.data), valor: d.entradas.toDouble(), dica: '${d.entradas} entradas'),
      ],
    );

/// Mensagem discreta de seção sem dados
class TextoVazio extends StatelessWidget {
  const TextoVazio(this.texto, {super.key});

  final String texto;

  @override
  Widget build(BuildContext context) => Text(texto, style: const TextStyle(color: CoresPkLots.textoSuave));
}
