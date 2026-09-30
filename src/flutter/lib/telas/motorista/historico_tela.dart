import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/motorista_provider.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/item_historico_tile.dart';

/// Acessos dos últimos 30 dias
class HistoricoTela extends StatelessWidget {
  const HistoricoTela({super.key});

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<MotoristaProvider>();
    final concluidos = estado.historico.where((h) => !h.emAndamento);
    final totalGasto = concluidos.fold<double>(0, (soma, h) => soma + (h.valorTotal ?? 0));

    return Scaffold(
      appBar: AppBar(title: const Text('Meus acessos')),
      body: RefreshIndicator(
        onRefresh: estado.carregar,
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(children: [
                      Expanded(child: _Resumo('Acessos (30 dias)', '${estado.historico.length}')),
                      Expanded(child: _Resumo('Total gasto', Formatadores.moeda(totalGasto))),
                    ]),
                  ),
                ),
                const SizedBox(height: 16),
                if (estado.historico.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(32),
                    child: Text('Nenhum acesso nos últimos 30 dias.',
                        textAlign: TextAlign.center, style: TextStyle(color: CoresPkLots.textoSecundario)),
                  )
                else
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                      child: Column(children: [
                        for (final (i, item) in estado.historico.indexed) ...[
                          if (i > 0) const Divider(height: 1),
                          ItemHistoricoTile(item: item),
                        ],
                      ]),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Resumo extends StatelessWidget {
  const _Resumo(this.rotulo, this.valor);

  final String rotulo;
  final String valor;

  @override
  Widget build(BuildContext context) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(rotulo, style: const TextStyle(color: CoresPkLots.textoSecundario)),
        const SizedBox(height: 4),
        Text(valor, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho)),
      ]);
}
