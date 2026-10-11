import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../servicos/relatorios_pdf.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import 'componentes_relatorio.dart';

/// Relatório do superadmin: uso da plataforma por empresa cliente
class RelatorioPlataformaTela extends StatefulWidget {
  const RelatorioPlataformaTela({super.key});

  /// Empresa ativa sem entrada há mais que isso aparece em destaque (risco de abandono)
  static const diasParaAlerta = 7;

  @override
  State<RelatorioPlataformaTela> createState() => _RelatorioPlataformaTelaState();
}

class _RelatorioPlataformaTelaState extends State<RelatorioPlataformaTela> {
  var _atalho = AtalhoPeriodo.esteMes;
  late PeriodoRelatorio _periodo;
  RelatorioPlataforma? _relatorio;
  String? _erro;
  bool _carregando = false;
  bool _exportando = false;
  int _consulta = 0;

  @override
  void initState() {
    super.initState();
    final agora = DateTime.now();
    _periodo = _atalho.periodo(DateTime(agora.year, agora.month, agora.day))!;
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  Future<void> _carregar() async {
    final consulta = ++_consulta;
    setState(() {
      _carregando = true;
      _erro = null;
    });
    try {
      final relatorio = await context.read<ClienteApi>().relatorioPlataforma(_periodo);
      if (mounted && consulta == _consulta) setState(() => _relatorio = relatorio);
    } on ExcecaoApi catch (e) {
      if (mounted && consulta == _consulta) setState(() => _erro = e.mensagem);
    } finally {
      if (mounted && consulta == _consulta) setState(() => _carregando = false);
    }
  }

  Future<void> _exportar(RelatorioPlataforma relatorio) async {
    setState(() => _exportando = true);
    try {
      await RelatoriosPdf.baixarPlataforma(relatorio);
    } catch (_) {
      if (mounted) mostrarAviso(context, 'Não foi possível gerar o PDF.', erro: true);
    } finally {
      if (mounted) setState(() => _exportando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final relatorio = _relatorio;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Relatório da plataforma'),
        actions: [
          if (relatorio != null)
            IconButton(
              tooltip: 'Baixar PDF',
              icon: _exportando
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.picture_as_pdf_outlined),
              onPressed: _exportando || _carregando ? null : () => _exportar(relatorio),
            ),
          IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: _carregando ? null : _carregar),
        ],
        bottom: _carregando && relatorio != null
            ? const PreferredSize(preferredSize: Size.fromHeight(2), child: LinearProgressIndicator(minHeight: 2))
            : null,
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1080),
          child: ListView(padding: const EdgeInsets.all(16), children: [
            SeletorPeriodo(
              atalho: _atalho,
              periodo: _periodo,
              aoMudar: (atalho, periodo) {
                setState(() {
                  _atalho = atalho;
                  _periodo = periodo;
                });
                _carregar();
              },
            ),
            const SizedBox(height: 16),
            if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
            if (relatorio == null && _erro == null)
              const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator())),
            if (relatorio != null) ..._conteudo(relatorio),
          ]),
        ),
      ),
    );
  }

  List<Widget> _conteudo(RelatorioPlataforma r) {
    final t = r.totais;
    const espaco = SizedBox(height: 12);
    final limiteAlerta = DateTime.now().subtract(const Duration(days: RelatorioPlataformaTela.diasParaAlerta));

    return [
      GradeIndicadores(itens: [
        CartaoIndicador(
            rotulo: 'Empresas ativas',
            valor: '${t.empresasAtivas}',
            icone: Icons.business,
            detalhe: '${t.empresasNovas} nova(s) no período'),
        CartaoIndicador(
            rotulo: 'Sem movimento',
            valor: '${t.empresasSemMovimento}',
            icone: Icons.pause_circle_outline,
            detalhe: 'ativas, sem entradas no período',
            alerta: t.empresasSemMovimento > 0),
        CartaoIndicador(rotulo: 'Pátios ativos', valor: '${t.patiosAtivos}', icone: Icons.store_mall_directory_outlined, detalhe: '${t.vagas} vagas'),
        CartaoIndicador(
            rotulo: 'Usuários ativos',
            valor: '${t.usuariosAtivos}',
            icone: Icons.people_outline,
            detalhe: '${t.mensalistasAtivos} mensalista(s)'),
        CartaoIndicador(rotulo: 'Entradas', valor: '${t.entradas}', icone: Icons.login),
        CartaoIndicador(
            rotulo: 'Valor processado',
            valor: Formatadores.moeda(t.valorProcessado),
            icone: Icons.payments_outlined,
            detalhe: 'saídas + mensalidades'),
      ]),
      espaco,
      if (r.porDia.length > 1) ...[
        SecaoRelatorio(
          titulo: 'Movimento da plataforma',
          icone: Icons.bar_chart,
          filho: graficoEntradasPorDia(r.porDia),
        ),
        espaco,
      ],
      SecaoRelatorio(
        titulo: 'Empresas',
        icone: Icons.business,
        subtitulo: 'Em destaque: ativas sem entradas há mais de ${RelatorioPlataformaTela.diasParaAlerta} dias.',
        filho: r.empresas.isEmpty
            ? const TextoVazio('Nenhuma empresa cadastrada.')
            : SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: DataTable(
                  headingRowHeight: 36,
                  dataRowMinHeight: 40,
                  dataRowMaxHeight: 52,
                  horizontalMargin: 8,
                  columnSpacing: 20,
                  columns: const [
                    DataColumn(label: Text('Empresa')),
                    DataColumn(label: Text('Pátios'), numeric: true),
                    DataColumn(label: Text('Vagas'), numeric: true),
                    DataColumn(label: Text('Usuários'), numeric: true),
                    DataColumn(label: Text('Mensalistas'), numeric: true),
                    DataColumn(label: Text('Entradas'), numeric: true),
                    DataColumn(label: Text('Valor processado'), numeric: true),
                    DataColumn(label: Text('Último movimento')),
                  ],
                  rows: [
                    for (final e in r.empresas)
                      DataRow(cells: [
                        DataCell(Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Text(e.nome,
                              style: TextStyle(
                                  fontWeight: FontWeight.w600, color: e.ativa ? CoresPkLots.azulMarinho : CoresPkLots.textoSuave)),
                          Text(e.ativa ? 'cliente desde ${Formatadores.data(e.criadoEm.toLocal())}' : 'Desativada',
                              style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                        ])),
                        DataCell(Text('${e.patios}')),
                        DataCell(Text('${e.vagas}')),
                        DataCell(Text('${e.usuarios}')),
                        DataCell(Text('${e.mensalistasAtivos}')),
                        DataCell(Text('${e.entradas}')),
                        DataCell(Text(Formatadores.moeda(e.valorProcessado))),
                        DataCell(_UltimoMovimento(
                            ultima: e.ultimaEntrada,
                            alerta: e.ativa && (e.ultimaEntrada == null || e.ultimaEntrada!.isBefore(limiteAlerta)))),
                      ]),
                  ],
                ),
              ),
      ),
    ];
  }
}

class _UltimoMovimento extends StatelessWidget {
  const _UltimoMovimento({required this.ultima, required this.alerta});

  final DateTime? ultima;
  final bool alerta;

  @override
  Widget build(BuildContext context) {
    final texto = ultima == null ? 'Nunca' : Formatadores.dataHoraCompleta(ultima!);
    if (!alerta) return Text(texto);
    return Row(mainAxisSize: MainAxisSize.min, children: [
      const Icon(Icons.warning_amber_rounded, size: 16, color: CoresPkLots.manutencao),
      const SizedBox(width: 4),
      Text(texto, style: const TextStyle(color: CoresPkLots.manutencao, fontWeight: FontWeight.w600)),
    ]);
  }
}
