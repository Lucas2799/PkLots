import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../servicos/relatorios_pdf.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/grafico_barras.dart';
import 'componentes_relatorio.dart';

/// Relatório do dono (admin): receita, movimento, pátios, operadores e cancelamentos no período
class RelatorioEmpresaTela extends StatefulWidget {
  const RelatorioEmpresaTela({super.key});

  @override
  State<RelatorioEmpresaTela> createState() => _RelatorioEmpresaTelaState();
}

class _RelatorioEmpresaTelaState extends State<RelatorioEmpresaTela> {
  var _atalho = AtalhoPeriodo.esteMes;
  late PeriodoRelatorio _periodo;
  String? _patioId; // nulo: todos os pátios
  List<PatioAdmin> _patios = [];
  RelatorioEmpresa? _relatorio;
  String? _erro;
  bool _carregando = false;
  bool _exportando = false;
  int _consulta = 0; // descarta respostas de filtros já trocados

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    final agora = DateTime.now();
    _periodo = _atalho.periodo(DateTime(agora.year, agora.month, agora.day))!;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _carregarPatios();
      _carregar();
    });
  }

  Future<void> _carregarPatios() async {
    try {
      final patios = await _api.patiosDaEmpresa();
      if (mounted) setState(() => _patios = patios);
    } on ExcecaoApi {
      // Sem a lista o relatório segue com todos os pátios
    }
  }

  Future<void> _carregar() async {
    final consulta = ++_consulta;
    setState(() {
      _carregando = true;
      _erro = null;
    });
    try {
      final relatorio = await _api.relatorioEmpresa(_periodo, patioId: _patioId);
      if (mounted && consulta == _consulta) setState(() => _relatorio = relatorio);
    } on ExcecaoApi catch (e) {
      if (mounted && consulta == _consulta) setState(() => _erro = e.mensagem);
    } finally {
      if (mounted && consulta == _consulta) setState(() => _carregando = false);
    }
  }

  Future<void> _exportar(RelatorioEmpresa relatorio) async {
    setState(() => _exportando = true);
    try {
      await RelatoriosPdf.baixarEmpresa(relatorio);
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
        title: const Text('Relatórios'),
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
          constraints: const BoxConstraints(maxWidth: 960),
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
            if (_patios.length > 1) ...[
              const SizedBox(height: 12),
              SizedBox(
                width: 320,
                child: DropdownButtonFormField<String?>(
                  initialValue: _patioId,
                  decoration: const InputDecoration(labelText: 'Pátio', prefixIcon: Icon(Icons.store_mall_directory_outlined)),
                  items: [
                    const DropdownMenuItem(value: null, child: Text('Todos os pátios')),
                    for (final p in _patios)
                      DropdownMenuItem(value: p.id, child: Text(p.ativo ? p.nome : '${p.nome} (inativo)')),
                  ],
                  onChanged: (id) {
                    setState(() => _patioId = id);
                    _carregar();
                  },
                ),
              ),
            ],
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

  List<Widget> _conteudo(RelatorioEmpresa r) {
    final t = r.totais;
    const espaco = SizedBox(height: 12);
    return [
      GradeIndicadores(itens: [
        CartaoIndicador(
            rotulo: 'Receita total',
            valor: Formatadores.moeda(t.receitaTotal),
            icone: Icons.payments_outlined,
            detalhe: 'Avulsos ${Formatadores.moeda(t.receitaAvulsos)}'),
        CartaoIndicador(
            rotulo: 'Mensalidades',
            valor: Formatadores.moeda(t.receitaMensalidades),
            icone: Icons.card_membership_outlined,
            detalhe: '${r.mensalistas.pagamentos} pagamento(s)'),
        CartaoIndicador(
            rotulo: 'Ticket médio',
            valor: Formatadores.moeda(t.ticketMedio),
            icone: Icons.receipt_long_outlined,
            detalhe: '${t.saidasCobradas} saída(s) cobrada(s)'),
        CartaoIndicador(
            rotulo: 'Permanência média', valor: t.permanenciaMedia, icone: Icons.timer_outlined, detalhe: '${t.saidas} saída(s)'),
        CartaoIndicador(rotulo: 'Entradas', valor: '${t.entradas}', icone: Icons.login),
        CartaoIndicador(
            rotulo: 'Sem cobrança',
            valor: '${t.saidasIsentas + t.saidasMensalistas}',
            icone: Icons.money_off_outlined,
            detalhe: '${t.saidasIsentas} tolerância · ${t.saidasMensalistas} mensalista'),
        CartaoIndicador(
            rotulo: 'Cancelamentos', valor: '${t.cancelamentos}', icone: Icons.block_outlined, alerta: t.cancelamentos > 0),
        CartaoIndicador(
            rotulo: 'Mensalistas vencidos',
            valor: '${r.mensalistas.vencidos}',
            icone: Icons.event_busy_outlined,
            detalhe: 'de ${r.mensalistas.ativos} ativo(s)',
            alerta: r.mensalistas.vencidos > 0),
      ]),
      espaco,
      if (r.porDia.length > 1) ...[
        SecaoRelatorio(
          titulo: 'Receita e movimento',
          icone: Icons.bar_chart,
          filho: Column(children: [
            graficoReceitaPorDia(r.porDia),
            const SizedBox(height: 16),
            graficoEntradasPorDia(r.porDia),
          ]),
        ),
        espaco,
      ],
      SecaoRelatorio(
        titulo: 'Horário de chegada',
        icone: Icons.schedule,
        subtitulo: 'Entradas por hora do dia, somando todo o período: ajuda a escalar a equipe.',
        filho: GraficoBarras(
          titulo: 'Entradas por hora',
          formatarEixo: (v) => v.round().toString(),
          barras: [
            for (var h = 0; h < 24; h++)
              BarraGrafico(rotulo: '${h}h', valor: r.entradasPorHora[h].toDouble(), dica: '${r.entradasPorHora[h]} entradas'),
          ],
        ),
      ),
      espaco,
      SecaoRelatorio(
        titulo: 'Formas de pagamento',
        icone: Icons.account_balance_wallet_outlined,
        filho: r.porForma.isEmpty
            ? const TextoVazio('Nenhum recebimento no período.')
            : TabelaRelatorio(
                colunas: const ['Forma', 'Quantidade', 'Total', '%'],
                numericas: const {1, 2, 3},
                linhas: [
                  for (final f in r.porForma)
                    [
                      rotulosFormaPagamento[f.forma] ?? 'Não informada',
                      '${f.quantidade}',
                      Formatadores.moeda(f.total),
                      Formatadores.percentual(t.receitaTotal == 0 ? 0 : (f.total * 1000 / t.receitaTotal).round() / 10),
                    ],
                ],
              ),
      ),
      espaco,
      if (r.porPatio.length > 1) ...[
        SecaoRelatorio(
          titulo: 'Comparativo dos pátios',
          icone: Icons.store_mall_directory_outlined,
          subtitulo: 'Giro: saídas por vaga por dia.',
          filho: TabelaRelatorio(
            colunas: const ['Pátio', 'Vagas', 'Entradas', 'Saídas', 'Giro', 'Avulsos', 'Mensalidades', 'Total'],
            numericas: const {1, 2, 3, 4, 5, 6, 7},
            linhas: [
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
            ],
          ),
        ),
        espaco,
      ],
      SecaoRelatorio(
        titulo: 'Operadores',
        icone: Icons.badge_outlined,
        subtitulo: 'Diferença de caixa: dinheiro contado menos o esperado, nos caixas fechados no período.',
        filho: r.porOperador.isEmpty
            ? const TextoVazio('Nenhum recebimento, caixa ou cancelamento no período.')
            : TabelaRelatorio(
                colunas: const ['Nome', 'Saídas', 'Mensalidades', 'Recebido', 'Caixas', 'Diferença', 'Cancelamentos'],
                numericas: const {1, 2, 3, 4, 5, 6},
                linhas: [
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
                ],
              ),
      ),
      espaco,
      SecaoRelatorio(
        titulo: 'Mensalistas',
        icone: Icons.card_membership_outlined,
        filho: GradeIndicadores(itens: [
          CartaoIndicador(rotulo: 'Ativos hoje', valor: '${r.mensalistas.ativos}', icone: Icons.people_outline),
          CartaoIndicador(rotulo: 'Em dia', valor: '${r.mensalistas.emDia}', icone: Icons.check_circle_outline),
          CartaoIndicador(
              rotulo: 'Vencidos', valor: '${r.mensalistas.vencidos}', icone: Icons.event_busy_outlined, alerta: r.mensalistas.vencidos > 0),
          CartaoIndicador(rotulo: 'Novos no período', valor: '${r.mensalistas.novos}', icone: Icons.person_add_alt),
        ]),
      ),
      espaco,
      SecaoRelatorio(
        titulo: 'Entradas canceladas',
        icone: Icons.block_outlined,
        filho: r.cancelamentos.isEmpty
            ? const TextoVazio('Nenhuma entrada cancelada no período.')
            : TabelaRelatorio(
                colunas: const ['Quando', 'Placa', 'Pátio', 'Cancelada por', 'Motivo'],
                linhas: [
                  for (final c in r.cancelamentos)
                    [
                      Formatadores.dataHoraCompleta(c.canceladaEm),
                      Formatadores.placa(c.placa),
                      c.nomePatio,
                      c.canceladaPor ?? '-',
                      c.motivo ?? '-',
                    ],
                ],
              ),
      ),
    ];
  }
}
