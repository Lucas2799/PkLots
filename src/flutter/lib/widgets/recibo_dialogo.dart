import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';
import '../util/formatadores.dart';

/// Recibo digital exibido após a saída
Future<void> mostrarRecibo(BuildContext context, Saida saida) => showDialog(
      context: context,
      builder: (context) => AlertDialog(
        icon: const Icon(Icons.receipt_long, color: CoresPkLots.ambar, size: 36),
        title: const Text('Saída registrada'),
        content: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 360),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(Formatadores.moeda(saida.valorTotal),
                style: const TextStyle(fontSize: 34, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho)),
            const SizedBox(height: 4),
            const Text('Valor calculado', style: TextStyle(color: CoresPkLots.textoSecundario)),
            const Divider(height: 28),
            _Linha('Placa', Formatadores.placa(saida.placa)),
            if (saida.numeroVaga != null) _Linha('Vaga', saida.numeroVaga!),
            _Linha('Entrada', Formatadores.dataHora(saida.dataHoraEntrada)),
            _Linha('Saída', Formatadores.dataHora(saida.dataHoraSaida)),
            _Linha('Permanência', saida.duracaoFormatada),
            _Linha('Valor/hora', Formatadores.moeda(saida.valorHora)),
          ]),
        ),
        actions: [FilledButton(onPressed: () => Navigator.of(context).pop(), child: const Text('OK'))],
      ),
    );

class _Linha extends StatelessWidget {
  const _Linha(this.rotulo, this.valor);

  final String rotulo;
  final String valor;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(children: [
          Expanded(child: Text(rotulo, style: const TextStyle(color: CoresPkLots.textoSecundario))),
          Text(valor, style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
        ]),
      );
}
