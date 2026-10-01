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
import '../../widgets/cartao_vagas.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/grafico_ocupacao.dart';
import '../../widgets/logo_pklots.dart';
import '../../widgets/recibo_dialogo.dart';
import '../admin/administracao_tela.dart';
import 'busca_placa_dialogo.dart';
import 'mapa_vagas_cartao.dart';
import 'movimentacao_cartao.dart';

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

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<GestaoProvider>();
    final sessao = context.watch<SessaoProvider>();
    final dashboard = estado.dashboard;

    return Scaffold(
      appBar: AppBar(
        title: Row(mainAxisSize: MainAxisSize.min, children: [
          const MarcaPkLots(altura: 28),
          if (dashboard != null) ...[
            const SizedBox(width: 12),
            Flexible(
              child: Text('· ${dashboard.nomePatio}',
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 16, color: CoresPkLots.textoSecundario)),
            ),
          ],
        ]),
        actions: [
          if (sessao.ehAdmin) _BotaoAdministracao(aoVoltar: () => estado.carregar(silencioso: true)),
          IconButton(
              tooltip: 'Histórico por placa', icon: const Icon(Icons.manage_search), onPressed: () => mostrarBuscaPlaca(context)),
          IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: estado.carregar),
          IconButton(tooltip: 'Sair', icon: const Icon(Icons.logout), onPressed: sessao.sair),
        ],
      ),
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
              const SizedBox(height: 16),
            ],
            const MovimentacaoCartao(),
            if (dashboard != null) ...[
              const SizedBox(height: 16),
              _Estacionados(veiculos: dashboard.estacionados),
            ],
          ];

          final cabecalho = <Widget>[
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

  Future<void> _registrarSaida(BuildContext context, String placa) async {
    try {
      final saida = await context.read<GestaoProvider>().registrarSaida(placa);
      if (context.mounted) await mostrarRecibo(context, saida);
    } on ExcecaoApi catch (e) {
      if (context.mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

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
                ]),
              ),
          ]),
        ),
      );
}

/// Acesso à administração: ícone de engrenagem, com o nome ao passar o mouse
class _BotaoAdministracao extends StatelessWidget {
  const _BotaoAdministracao({required this.aoVoltar});

  /// Preço, vagas ou nome do pátio podem ter mudado
  final Future<void> Function() aoVoltar;

  @override
  Widget build(BuildContext context) => IconButton(
        tooltip: 'Administração',
        icon: const Icon(Icons.settings_outlined),
        onPressed: () async {
          await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const AdministracaoTela()));
          await aoVoltar();
        },
      );
}
