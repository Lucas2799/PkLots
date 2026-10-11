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
import '../troca_senha_tela.dart';
import 'historico_tela.dart';

/// Tela principal do mensalista (perfil motorista)
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
                  if (estado.contratos.isEmpty) const _SemContrato(),
                  for (final contrato in estado.contratos) ...[
                    _Contrato(contrato: contrato),
                    const SizedBox(height: 16),
                  ],
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

/// Contrato de mensalista: validade da mensalidade, placas e veículos estacionados agora
class _Contrato extends StatelessWidget {
  const _Contrato({required this.contrato});

  final ContratoMensalista contrato;

  @override
  Widget build(BuildContext context) {
    final c = contrato;
    final vencido = c.situacao == Mensalista.vencido;
    final (texto, cor) = switch (c.situacao) {
      Mensalista.emDia => ('Em dia', CoresPkLots.disponivel),
      Mensalista.vencendo => ('Vence em breve', CoresPkLots.ambarEscuro),
      _ => ('Vencida', CoresPkLots.erro),
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Row(children: [
            Expanded(child: TituloSecao(c.nomePatio, icone: Icons.card_membership_outlined)),
            Chip(
              label: Text(texto, style: TextStyle(color: cor, fontWeight: FontWeight.w700)),
              side: BorderSide(color: cor),
              backgroundColor: Colors.transparent,
            ),
          ]),
          Text(c.nomeEmpresa, style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
          const SizedBox(height: 8),
          _Info('Mensalidade', c.validoAte == null ? 'Sem pagamento' : 'até ${Formatadores.data(c.validoAte!)}',
              destaque: true),
          if (vencido)
            const Padding(
              padding: EdgeInsets.only(top: 4),
              child: Text('Enquanto estiver vencida, a estadia é cobrada como avulsa. Regularize no pátio.',
                  style: TextStyle(color: CoresPkLots.erro)),
            ),
          const SizedBox(height: 8),
          Wrap(spacing: 8, runSpacing: 8, children: [
            for (final placa in c.placas)
              Chip(avatar: const Icon(Icons.directions_car, size: 18), label: Text(Formatadores.placa(placa))),
          ]),
          for (final e in c.estacionados) ...[
            const Divider(height: 28),
            Text('Estacionado agora', style: Theme.of(context).textTheme.titleSmall),
            _Info('Placa', Formatadores.placa(e.placa), destaque: true),
            _Info('Entrada', Formatadores.dataHora(e.dataHoraEntrada)),
            _Info('Estacionado há', e.duracao),
            if (e.numeroVaga != null) _Info('Vaga', e.numeroVaga!),
            _Info(e.valorEstimado == 0 ? 'Valor' : 'Valor estimado',
                e.valorEstimado == 0 ? 'Coberto pela mensalidade' : Formatadores.moeda(e.valorEstimado)),
          ],
        ]),
      ),
    );
  }
}

/// Conta sem contrato ativo (desativado pelo pátio ou e-mail trocado)
class _SemContrato extends StatelessWidget {
  const _SemContrato();

  @override
  Widget build(BuildContext context) => const Card(
        child: Padding(
          padding: EdgeInsets.all(20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            TituloSecao('Minha mensalidade', icone: Icons.card_membership_outlined),
            Text(
              'Nenhum contrato de mensalista ativo nesta conta. O acesso é liberado pelo estacionamento, '
              'por convite enviado ao seu e-mail.',
              style: TextStyle(color: CoresPkLots.textoSecundario),
            ),
            SizedBox(height: 8),
            Text(
              'Estacionou como avulso? Leia o QR do ticket com a câmera do celular para acompanhar o tempo e o valor.',
              style: TextStyle(color: CoresPkLots.textoSecundario),
            ),
          ]),
        ),
      );
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
              child: Text('As estadias dos últimos 30 dias nos pátios dos seus contratos aparecem aqui.',
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
