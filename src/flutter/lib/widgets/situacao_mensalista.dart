import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';

/// Situação do mensalista: cor + ícone + texto (nunca só a cor)
class EtiquetaMensalista extends StatelessWidget {
  const EtiquetaMensalista({super.key, required this.mensalista});

  final Mensalista mensalista;

  static (Color, IconData, String) aparencia(String situacao, int? dias) => switch (situacao) {
        Mensalista.emDia => (CoresPkLots.disponivel, Icons.check_circle_outline, 'Em dia'),
        Mensalista.vencendo => (
            CoresPkLots.manutencao,
            Icons.schedule,
            dias == 0 ? 'Vence hoje' : 'Vence em $dias dia${dias == 1 ? '' : 's'}'
          ),
        Mensalista.vencido => (CoresPkLots.erro, Icons.error_outline, 'Vencido'),
        _ => (CoresPkLots.textoSuave, Icons.block, 'Inativo'),
      };

  @override
  Widget build(BuildContext context) {
    final (cor, icone, rotulo) = aparencia(mensalista.situacao, mensalista.diasParaVencer);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: cor.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(20)),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(icone, size: 14, color: cor),
        const SizedBox(width: 4),
        Text(rotulo, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
      ]),
    );
  }
}
