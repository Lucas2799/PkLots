import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';
import '../util/formatadores.dart';

/// Linha de histórico: data, placa/vaga, duração e valor
class ItemHistoricoTile extends StatelessWidget {
  const ItemHistoricoTile({super.key, required this.item, this.mostrarPlaca = true});

  final ItemHistorico item;
  final bool mostrarPlaca;

  @override
  Widget build(BuildContext context) {
    final detalhes = [
      if (mostrarPlaca) Formatadores.placa(item.placa),
      ?item.nomePatio,
      if (item.numeroVaga != null) 'Vaga ${item.numeroVaga}',
      'Entrada ${Formatadores.hora(item.dataHoraEntrada)}',
    ].join(' · ');

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: CircleAvatar(
        backgroundColor: CoresPkLots.fundo,
        child: Icon(item.emAndamento ? Icons.timelapse : Icons.check,
            color: item.emAndamento ? CoresPkLots.ambarEscuro : CoresPkLots.azulMarinho),
      ),
      title: Text(Formatadores.dataCurta(item.dataHoraEntrada),
          style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
      subtitle: Text(detalhes),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: item.emAndamento
            ? const [Text('Em andamento', style: TextStyle(color: CoresPkLots.ambarEscuro, fontWeight: FontWeight.w600))]
            : [
                Text(Formatadores.moeda(item.valorTotal ?? 0),
                    style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                Text(item.duracaoFormatada ?? '', style: const TextStyle(color: CoresPkLots.textoSecundario)),
              ],
      ),
    );
  }
}
