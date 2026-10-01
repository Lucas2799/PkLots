import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/motorista_provider.dart';
import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/item_historico_tile.dart';
import '../../widgets/logo_pklots.dart';
import 'cadastro_veiculo_dialogo.dart';
import '../troca_senha_tela.dart';
import 'historico_tela.dart';

/// Tela principal do motorista
class InicioMotoristaTela extends StatefulWidget {
  const InicioMotoristaTela({super.key});

  @override
  State<InicioMotoristaTela> createState() => _InicioMotoristaTelaState();
}

class _InicioMotoristaTelaState extends State<InicioMotoristaTela> {
  late final MotoristaProvider _estado;

  @override
  void initState() {
    super.initState();
    _estado = context.read<MotoristaProvider>();
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
    final estado = context.watch<MotoristaProvider>();
    final sessao = context.watch<SessaoProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const MarcaPkLots(altura: 28),
        actions: [
          if (sessao.primeiroNome.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(right: 4),
              child: Center(child: Text('Olá, ${sessao.primeiroNome}', style: const TextStyle(fontWeight: FontWeight.w600))),
            ),
          IconButton(
            tooltip: 'Alterar senha',
            icon: const Icon(Icons.key_outlined),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TrocaSenhaTela())),
          ),
          IconButton(tooltip: 'Sair', icon: const Icon(Icons.logout), onPressed: sessao.sair),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: estado.carregar,
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 600),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (estado.carregando && estado.patios == null)
                  const Padding(padding: EdgeInsets.all(48), child: Center(child: CircularProgressIndicator())),
                if (estado.erro != null) ...[
                  MensagemErro(mensagem: estado.erro!, aoTentarNovamente: estado.carregar),
                  const SizedBox(height: 16),
                ],
                if (estado.patios != null) ...[
                  _MeuEstacionamento(estado: estado),
                  const SizedBox(height: 16),
                  _Patios(patios: estado.patios!),
                  const SizedBox(height: 16),
                  _MeusAcessos(historico: estado.historico),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _MeuEstacionamento extends StatefulWidget {
  const _MeuEstacionamento({required this.estado});

  final MotoristaProvider estado;

  @override
  State<_MeuEstacionamento> createState() => _MeuEstacionamentoState();
}

class _MeuEstacionamentoState extends State<_MeuEstacionamento> {
  @override
  Widget build(BuildContext context) {
    final atual = widget.estado.atual;
    final veiculos = widget.estado.veiculos;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const TituloSecao('Meu estacionamento', icone: Icons.directions_car),
            if (atual != null) ...[
              if (atual.nomePatio.isNotEmpty) _Info('Pátio', atual.nomePatio),
              _Info('Placa', Formatadores.placa(atual.placa), destaque: true),
              _Info('Entrada', Formatadores.dataHora(atual.dataHoraEntrada)),
              _Info('Estacionado há', atual.duracao),
              if (atual.numeroVaga != null) _Info('Vaga', atual.numeroVaga!),
              _Info('Valor estimado', Formatadores.moeda(atual.valorEstimado), destaque: true),
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: CoresPkLots.ambar.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Row(children: [
                  Icon(Icons.point_of_sale, color: CoresPkLots.azulMarinho),
                  SizedBox(width: 10),
                  Expanded(
                    child: Text('O pagamento é feito na saída do pátio. O valor final é calculado na hora da saída.',
                        style: TextStyle(color: CoresPkLots.azulMarinho)),
                  ),
                ]),
              ),
            ] else if (veiculos.isEmpty) ...[
              const Text('Cadastre a placa do seu veículo para acompanhar o estacionamento por aqui.',
                  style: TextStyle(color: CoresPkLots.textoSecundario)),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: () => mostrarCadastroVeiculo(context),
                icon: const Icon(Icons.add),
                label: const Text('Cadastrar veículo'),
              ),
            ] else ...[
              const Text('Nenhum veículo estacionado no momento.', style: TextStyle(color: CoresPkLots.textoSecundario)),
              const SizedBox(height: 12),
              Wrap(spacing: 8, runSpacing: 8, children: [
                for (final v in veiculos)
                  Chip(
                    avatar: const Icon(Icons.directions_car, size: 18),
                    label: Text([Formatadores.placa(v.placa), if (v.modelo != null) v.modelo].join(' · ')),
                  ),
                ActionChip(
                  avatar: const Icon(Icons.add, size: 18),
                  label: const Text('Adicionar'),
                  onPressed: () => mostrarCadastroVeiculo(context),
                ),
              ]),
            ],
          ],
        ),
      ),
    );
  }
}

class _Info extends StatelessWidget {
  const _Info(this.rotulo, this.valor, {this.destaque = false});

  final String rotulo;
  final String valor;
  final bool destaque;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(children: [
          Expanded(child: Text(rotulo, style: const TextStyle(color: CoresPkLots.textoSecundario))),
          Text(valor,
              style: TextStyle(
                fontSize: destaque ? 18 : 15,
                fontWeight: destaque ? FontWeight.w800 : FontWeight.w600,
                color: CoresPkLots.azulMarinho,
              )),
        ]),
      );
}

class _MeusAcessos extends StatelessWidget {
  const _MeusAcessos({required this.historico});

  final List<ItemHistorico> historico;

  @override
  Widget build(BuildContext context) {
    final recentes = historico.take(3).toList();
    return Card(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
        child: Column(children: [
          TituloSecao(
            'Meus acessos',
            icone: Icons.history,
            acao: historico.isEmpty
                ? null
                : TextButton(
                    onPressed: () =>
                        Navigator.of(context).push(MaterialPageRoute(builder: (_) => const HistoricoTela())),
                    child: const Text('Ver todos'),
                  ),
          ),
          if (recentes.isEmpty)
            const Padding(
              padding: EdgeInsets.only(bottom: 12),
              child: Text('Seus acessos dos últimos 30 dias aparecem aqui.',
                  style: TextStyle(color: CoresPkLots.textoSecundario)),
            ),
          for (final item in recentes) ItemHistoricoTile(item: item),
        ]),
      ),
    );
  }
}

/// Pátios da plataforma com as vagas livres agora
class _Patios extends StatelessWidget {
  const _Patios({required this.patios});

  final List<PatioDisponibilidade> patios;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const TituloSecao('Vagas disponíveis', icone: Icons.local_parking),
            if (patios.isEmpty)
              const Padding(
                padding: EdgeInsets.only(bottom: 8),
                child: Text('Nenhum pátio disponível no momento.', style: TextStyle(color: CoresPkLots.textoSecundario)),
              ),
            for (final patio in patios)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Semantics(
                  label: '${patio.nome}: ${patio.disponiveis} de ${patio.total} vagas disponíveis',
                  excludeSemantics: true,
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Row(children: [
                      Expanded(
                        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                          Text(patio.nome,
                              style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                          Text(patio.nomeEmpresa, style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                        ]),
                      ),
                      Text.rich(TextSpan(children: [
                        TextSpan(
                          text: '${patio.disponiveis}',
                          style: TextStyle(
                            fontSize: 22,
                            fontWeight: FontWeight.w800,
                            color: patio.disponiveis == 0 ? CoresPkLots.erro : CoresPkLots.azulMarinho,
                          ),
                        ),
                        TextSpan(text: ' / ${patio.total}', style: const TextStyle(color: CoresPkLots.textoSuave)),
                      ])),
                    ]),
                    const SizedBox(height: 6),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                        value: patio.total == 0 ? 0 : patio.disponiveis / patio.total,
                        minHeight: 6,
                        backgroundColor: CoresPkLots.borda,
                        color: patio.disponiveis == 0 ? CoresPkLots.erro : CoresPkLots.ambar,
                      ),
                    ),
                    if (patio.disponiveis == 0)
                      const Padding(
                        padding: EdgeInsets.only(top: 4),
                        child: Text('Lotado', style: TextStyle(color: CoresPkLots.erro, fontWeight: FontWeight.w600)),
                      ),
                  ]),
                ),
              ),
          ]),
        ),
      );
}
