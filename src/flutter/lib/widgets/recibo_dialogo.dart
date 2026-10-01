import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../servicos/impressao_tickets.dart';
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
            Text(
                saida.mensalistaSemCobranca
                    ? 'Mensalista${saida.mensalista != null ? ' ${saida.mensalista}' : ''}: sem cobrança'
                    : saida.isento
                        ? 'Isento: dentro da tolerância gratuita'
                        : 'Valor calculado',
                textAlign: TextAlign.center,
                style: const TextStyle(color: CoresPkLots.textoSecundario)),
            if (saida.aviso != null) ...[
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: CoresPkLots.manutencao.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(children: [
                  const Icon(Icons.warning_amber_rounded, color: CoresPkLots.manutencao),
                  const SizedBox(width: 8),
                  Expanded(child: Text(saida.aviso!)),
                ]),
              ),
            ],
            const Divider(height: 28),
            _Linha('Placa', Formatadores.placa(saida.placa)),
            if (saida.numeroVaga != null) _Linha('Vaga', saida.numeroVaga!),
            _Linha('Entrada', Formatadores.dataHora(saida.dataHoraEntrada)),
            _Linha('Saída', Formatadores.dataHora(saida.dataHoraSaida)),
            _Linha('Permanência', saida.duracaoFormatada),
            _Linha('Valor/hora', Formatadores.moeda(saida.valorHora)),
          ]),
        ),
        actions: [
          TextButton.icon(
            onPressed: () async {
              try {
                await ImpressaoTickets.baixarRecibo(saida);
              } catch (_) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context)
                      .showSnackBar(const SnackBar(content: Text('Não foi possível gerar o PDF do recibo.')));
                }
              }
            },
            icon: const Icon(Icons.picture_as_pdf_outlined),
            label: const Text('Baixar PDF'),
          ),
          SizedBox(
            width: 100,
            child: FilledButton(onPressed: () => Navigator.of(context).pop(), child: const Text('OK')),
          ),
        ],
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
