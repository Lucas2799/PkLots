import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';

/// Aparência de cada situação de vaga: cor + ícone + texto (nunca só a cor)
class AparenciaSituacao {
  final Color cor;
  final IconData icone;
  final String rotulo;

  const AparenciaSituacao(this.cor, this.icone, this.rotulo);

  static AparenciaSituacao de(String situacao) => switch (situacao) {
        Vaga.disponivel => const AparenciaSituacao(CoresPkLots.disponivel, Icons.check_circle_outline, 'Disponível'),
        Vaga.ocupada => const AparenciaSituacao(CoresPkLots.ocupada, Icons.directions_car, 'Ocupada'),
        Vaga.manutencao => const AparenciaSituacao(CoresPkLots.manutencao, Icons.build_outlined, 'Manutenção'),
        _ => const AparenciaSituacao(CoresPkLots.textoSuave, Icons.help_outline, 'Desconhecida'),
      };
}

class EtiquetaSituacao extends StatelessWidget {
  const EtiquetaSituacao({super.key, required this.situacao});

  final String situacao;

  @override
  Widget build(BuildContext context) {
    final aparencia = AparenciaSituacao.de(situacao);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: aparencia.cor.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(aparencia.icone, size: 16, color: aparencia.cor),
          const SizedBox(width: 4),
          Text(aparencia.rotulo,
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
        ],
      ),
    );
  }
}
