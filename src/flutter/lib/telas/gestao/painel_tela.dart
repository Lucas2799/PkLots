import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/impressao_tickets.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/aviso_cobranca.dart';
import '../../widgets/cartao_vagas.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/grafico_ocupacao.dart';
import '../../widgets/logo_pklots.dart';
import '../admin/administracao_tela.dart';
import '../relatorios/relatorio_empresa_tela.dart';
import '../troca_senha_tela.dart';
import 'caixa_tela.dart';
import 'cancelar_entrada_dialogo.dart';
import 'cobranca_dialogo.dart';
import '../mensalistas/mensalistas_tela.dart';
import 'busca_placa_dialogo.dart';
import 'mapa_vagas_cartao.dart';
import 'movimentacao_cartao.dart';

/// Abaixo desta largura as ações ficam numa barra inferior (celular)
const larguraBarraInferior = 720.0;

/// Painel do gerente/operador
class PainelTela extends StatefulWidget {
  const PainelTela({super.key});

  @override
  State<PainelTela> createState() => _PainelTelaState();
}

class _PainelTelaState extends State<PainelTela> {
  late final GestaoProvider _estado;

  @override
  void initState() {
    super.initState();
    _estado = context.read<GestaoProvider>();
    // Inicia após o primeiro quadro: o carregamento notifica ouvintes
    WidgetsBinding.instance.addPostFrameCallback((_) => _estado.iniciar());
  }

  @override
  void dispose() {
    _estado.parar();
    super.dispose();
  }

  Future<void> _abrirAdministracao() async {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const AdministracaoTela()));
    // Pátios podem ter sido criados/renomeados; preço e vagas podem ter mudado
    await _estado.carregarPatios();
    await _estado.carregar(silencioso: true);
  }

  void _abrirRelatorios() =>
      Navigator.of(context).push(MaterialPageRoute(builder: (_) => const RelatorioEmpresaTela()));

  Future<void> _abrirCaixa() async {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const CaixaTela()));
    await _estado.carregar(silencioso: true);
  }

  void _abrirTrocaSenha() =>
      Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TrocaSenhaTela()));

  /// Ações menos usadas da barra inferior
  Future<void> _mostrarMais(SessaoProvider sessao) async {
    final acao = await showModalBottomSheet<VoidCallback>(
      context: context,
      showDragHandle: true,
      builder: (contexto) {
        ListTile opcao(IconData icone, String titulo, VoidCallback aoTocar) => ListTile(
              leading: Icon(icone),
              title: Text(titulo),
              onTap: () => Navigator.of(contexto).pop(aoTocar),
            );
        return SafeArea(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            if (sessao.ehAdmin) opcao(Icons.settings_outlined, 'Administração', _abrirAdministracao),
            if (sessao.ehAdmin && _estado.temRecurso(Recursos.relatorios))
              opcao(Icons.insights_outlined, 'Relatórios', _abrirRelatorios),
            opcao(Icons.manage_search, 'Histórico por placa', () => mostrarBuscaPlaca(context)),
            opcao(Icons.key_outlined, 'Alterar senha', _abrirTrocaSenha),
            const Divider(height: 1),
            opcao(Icons.logout, 'Sair', sessao.sair),
          ]),
        );
      },
    );
    // Executa depois de fechar a folha, com o contexto da tela
    if (acao != null && mounted) acao();
  }

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<GestaoProvider>();
    final sessao = context.watch<SessaoProvider>();
    final dashboard = estado.dashboard;

    // Celular: as ações vão para a barra inferior; telas largas mantêm os ícones no topo
    final estreito = MediaQuery.sizeOf(context).width < larguraBarraInferior;

    return Scaffold(
      appBar: AppBar(
        title: Row(mainAxisSize: MainAxisSize.min, children: [
          const MarcaPkLots(altura: 28),
          const SizedBox(width: 12),
          Flexible(child: _SeletorPatio(estado: estado)),
        ]),
        actions: estreito
            ? null
            : [
                if (sessao.ehAdmin)
                  IconButton(
                    tooltip: 'Administração',
                    icon: const Icon(Icons.settings_outlined),
                    onPressed: _abrirAdministracao,
                  ),
                if (sessao.ehAdmin && estado.temRecurso(Recursos.relatorios))
                  IconButton(tooltip: 'Relatórios', icon: const Icon(Icons.insights_outlined), onPressed: _abrirRelatorios),
                IconButton(
                  tooltip: estado.caixa == null ? 'Caixa (fechado)' : 'Caixa (aberto)',
                  icon: _IconeCaixa(aberto: estado.caixa != null),
                  onPressed: _abrirCaixa,
                ),
                IconButton(
                  tooltip: 'Mensalistas',
                  icon: const Icon(Icons.card_membership_outlined),
                  onPressed: () => _abrirMensalistas(context, estado),
                ),
                IconButton(
                    tooltip: 'Histórico por placa',
                    icon: const Icon(Icons.manage_search),
                    onPressed: () => mostrarBuscaPlaca(context)),
                IconButton(tooltip: 'Alterar senha', icon: const Icon(Icons.key_outlined), onPressed: _abrirTrocaSenha),
                IconButton(tooltip: 'Sair', icon: const Icon(Icons.logout), onPressed: sessao.sair),
              ],
      ),
      bottomNavigationBar: estreito
          ? NavigationBar(
              selectedIndex: 0,
              labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
              onDestinationSelected: (indice) => switch (indice) {
                1 => _abrirCaixa(),
                2 => _abrirMensalistas(context, estado),
                3 => _mostrarMais(sessao),
                _ => estado.carregar(),
              },
              destinations: [
                const NavigationDestination(
                    icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: 'Painel'),
                NavigationDestination(
                  icon: _IconeCaixa(aberto: estado.caixa != null),
                  label: 'Caixa',
                  tooltip: estado.caixa == null ? 'Caixa (fechado)' : 'Caixa (aberto)',
                ),
                const NavigationDestination(icon: Icon(Icons.card_membership_outlined), label: 'Mensalistas'),
                const NavigationDestination(icon: Icon(Icons.menu), label: 'Mais'),
              ],
            )
          : null,
      body: RefreshIndicator(
        onRefresh: estado.carregar,
        child: LayoutBuilder(builder: (context, restricoes) {
          final largo = restricoes.maxWidth >= 1000;

          final colunaPrincipal = <Widget>[
            if (dashboard != null) ...[
              _ResumoHoje(resumo: dashboard.hoje),
              const SizedBox(height: 16),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    const TituloSecao('Ocupação', icone: Icons.bar_chart),
                    GraficoOcupacao(pontos: dashboard.ocupacao24h, totalVagas: dashboard.vagas.total),
                  ]),
                ),
              ),
              const SizedBox(height: 16),
              MapaVagasCartao(vagas: estado.vagas),
            ],
          ];

          final colunaLateral = <Widget>[
            if (dashboard != null) ...[
              CartaoVagas(resumo: dashboard.vagas, mostrarDetalhes: true),
              if (dashboard.mensalistas case final mensalistas? when mensalistas.ativos > 0 || mensalistas.vagasReservadas > 0) ...[
                const SizedBox(height: 16),
                _ResumoMensalistas(resumo: mensalistas, aoAbrir: () => _abrirMensalistas(context, estado)),
              ],
              const SizedBox(height: 16),
            ],
            const MovimentacaoCartao(),
            if (dashboard != null) ...[
              const SizedBox(height: 16),
              _Estacionados(veiculos: dashboard.estacionados),
            ],
          ];

          final cabecalho = <Widget>[
            AvisoCobranca(cobranca: estado.cobranca, ehAdmin: sessao.ehAdmin),
            if (estado.carregando && dashboard == null)
              const Padding(padding: EdgeInsets.all(48), child: Center(child: CircularProgressIndicator())),
            if (estado.erro != null) ...[
              MensagemErro(mensagem: estado.erro!, aoTentarNovamente: estado.carregar),
              const SizedBox(height: 16),
            ],
          ];

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 1280),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    ...cabecalho,
                    if (largo)
                      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Expanded(flex: 3, child: Column(children: colunaPrincipal)),
                        const SizedBox(width: 16),
                        Expanded(flex: 2, child: Column(children: colunaLateral)),
                      ])
                    else ...[
                      // Em telas estreitas, as ações do operador vêm primeiro
                      ...colunaLateral,
                      const SizedBox(height: 16),
                      ...colunaPrincipal,
                    ],
                  ]),
                ),
              ),
            ],
          );
        }),
      ),
    );
  }
}

class _ResumoHoje extends StatelessWidget {
  const _ResumoHoje({required this.resumo});

  final ResumoDia resumo;

  @override
  Widget build(BuildContext context) {
    final indicadores = [
      ('Entradas', '${resumo.entradas}', Icons.login),
      ('Saídas', '${resumo.saidas}', Icons.logout),
      ('Receita', Formatadores.moeda(resumo.receita), Icons.payments_outlined),
      ('Média de estadia', resumo.mediaEstadia, Icons.schedule),
    ];

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const TituloSecao('Resumo de hoje', icone: Icons.today),
          LayoutBuilder(builder: (context, restricoes) {
            final colunas = restricoes.maxWidth >= 520 ? 4 : 2;
            final largura = (restricoes.maxWidth - (colunas - 1) * 12) / colunas;
            return Wrap(spacing: 12, runSpacing: 12, children: [
              for (final (rotulo, valor, icone) in indicadores)
                SizedBox(
                  width: largura,
                  child: Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: CoresPkLots.fundo,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Row(children: [
                        Icon(icone, size: 16, color: CoresPkLots.textoSecundario),
                        const SizedBox(width: 6),
                        Flexible(
                          child: Text(rotulo,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontSize: 13, color: CoresPkLots.textoSecundario)),
                        ),
                      ]),
                      const SizedBox(height: 6),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: Text(valor,
                            style: const TextStyle(
                                fontSize: 24, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho)),
                      ),
                    ]),
                  ),
                ),
            ]);
          }),
        ]),
      ),
    );
  }
}

class _Estacionados extends StatelessWidget {
  const _Estacionados({required this.veiculos});

  final List<VeiculoEstacionado> veiculos;

  Future<void> _reimprimir(BuildContext context, VeiculoEstacionado veiculo) async {
    try {
      final naRede = await ImpressaoTickets(context.read<ClienteApi>()).reimprimir(veiculo.entradaId);
      if (naRede && context.mounted) mostrarAviso(context, 'Ticket de ${Formatadores.placa(veiculo.placa)} enviado à impressora');
    } on ExcecaoApi catch (e) {
      if (context.mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _cancelar(BuildContext context, VeiculoEstacionado veiculo) async {
    final cancelou = await mostrarCancelarEntrada(context, veiculo);
    if (cancelou == true && context.mounted) {
      mostrarAviso(context, 'Entrada de ${Formatadores.placa(veiculo.placa)} cancelada');
    }
  }

  Future<void> _registrarSaida(BuildContext context, String placa) =>
      processarSaida(context, () => context.read<GestaoProvider>().cobrarSaida(placa));

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            TituloSecao('Estacionados agora (${veiculos.length})', icone: Icons.directions_car),
            if (veiculos.isEmpty)
              const Padding(
                padding: EdgeInsets.only(bottom: 12),
                child: Text('Nenhum veículo no pátio.', style: TextStyle(color: CoresPkLots.textoSecundario)),
              ),
            for (final v in veiculos)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: CircleAvatar(
                  backgroundColor: CoresPkLots.azulMarinho,
                  child: Text(v.numeroVaga ?? '-',
                      style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.w700)),
                ),
                title: Text(Formatadores.placa(v.placa),
                    style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                subtitle: Text('Entrada ${Formatadores.hora(v.dataHoraEntrada)} · ${v.duracao}'),
                trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                  IconButton(
                    tooltip: 'Reimprimir ticket de ${v.placa}',
                    icon: const Icon(Icons.print_outlined),
                    onPressed: () => _reimprimir(context, v),
                  ),
                  IconButton(
                    tooltip: 'Registrar saída de ${v.placa}',
                    icon: const Icon(Icons.logout),
                    onPressed: () => _registrarSaida(context, v.placa),
                  ),
                  IconButton(
                    tooltip: 'Cancelar entrada de ${v.placa}',
                    icon: const Icon(Icons.cancel_outlined),
                    onPressed: () => _cancelar(context, v),
                  ),
                ]),
              ),
          ]),
        ),
      );
}

/// Acesso à administração: ícone de engrenagem, com o nome ao passar o mouse
/// Ícone do caixa com um ponto verde quando aberto
class _IconeCaixa extends StatelessWidget {
  const _IconeCaixa({required this.aberto});

  final bool aberto;

  @override
  Widget build(BuildContext context) => Badge(
        isLabelVisible: aberto,
        backgroundColor: CoresPkLots.disponivel,
        smallSize: 8,
        child: const Icon(Icons.point_of_sale),
      );
}

/// Nome do pátio atual; com mais de um pátio vira um menu para trocar
class _SeletorPatio extends StatelessWidget {
  const _SeletorPatio({required this.estado});

  final GestaoProvider estado;

  @override
  Widget build(BuildContext context) {
    final atual = estado.patioAtual;
    if (atual == null) return const SizedBox.shrink();

    final rotulo = Text('· ${atual.nome}',
        overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 16, color: CoresPkLots.textoSecundario));
    if (estado.patios.length < 2) return rotulo;

    return PopupMenuButton<String>(
      tooltip: 'Trocar de pátio',
      initialValue: atual.id,
      onSelected: estado.selecionarPatio,
      itemBuilder: (_) => [
        for (final patio in estado.patios)
          PopupMenuItem(
            value: patio.id,
            child: Row(children: [
              Icon(patio.id == atual.id ? Icons.radio_button_checked : Icons.radio_button_unchecked, size: 18),
              const SizedBox(width: 8),
              Text(patio.nome),
            ]),
          ),
      ],
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Flexible(child: rotulo),
        const Icon(Icons.arrow_drop_down, color: CoresPkLots.textoSecundario),
      ]),
    );
  }
}

Future<void> _abrirMensalistas(BuildContext context, GestaoProvider estado) async {
  await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const MensalistasTela()));
  await estado.carregar(silencioso: true);
}

/// Mensalistas do pátio: em dia, vencendo, vencidos e estacionados
class _ResumoMensalistas extends StatelessWidget {
  const _ResumoMensalistas({required this.resumo, required this.aoAbrir});

  final ResumoMensalistas resumo;
  final VoidCallback aoAbrir;

  @override
  Widget build(BuildContext context) => Card(
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: aoAbrir,
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              TituloSecao('Mensalistas (${resumo.ativos})',
                  icone: Icons.card_membership_outlined,
                  acao: TextButton(onPressed: aoAbrir, child: const Text('Ver todos'))),
              Wrap(spacing: 16, runSpacing: 8, children: [
                _Indicador(Icons.check_circle_outline, 'Em dia', resumo.emDia, CoresPkLots.disponivel),
                _Indicador(Icons.schedule, 'Vencendo', resumo.vencendo, CoresPkLots.manutencao),
                _Indicador(Icons.error_outline, 'Vencidos', resumo.vencidos, CoresPkLots.erro),
                _Indicador(Icons.local_parking, 'No pátio', resumo.estacionados, CoresPkLots.azulMarinho),
              ]),
              if (resumo.vagasReservadas > 0) ...[
                const SizedBox(height: 10),
                Text('${resumo.reservadasLivres} de ${resumo.vagasReservadas} vagas reservadas livres',
                    style: const TextStyle(fontSize: 13, color: CoresPkLots.textoSecundario)),
              ],
            ]),
          ),
        ),
      );
}

class _Indicador extends StatelessWidget {
  const _Indicador(this.icone, this.rotulo, this.valor, this.cor);

  final IconData icone;
  final String rotulo;
  final int valor;
  final Color cor;

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(icone, size: 18, color: cor),
        const SizedBox(width: 4),
        Text('$rotulo: ', style: const TextStyle(color: CoresPkLots.textoSecundario)),
        Text('$valor', style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
      ]);
}
