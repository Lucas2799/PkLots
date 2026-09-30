import 'package:flutter/material.dart';

import '../../tema/cores.dart';

/// Nome e ícone de cada perfil de usuário
class InfoPerfil {
  final String rotulo;
  final String descricao;
  final IconData icone;

  const InfoPerfil(this.rotulo, this.descricao, this.icone);

  static InfoPerfil de(String perfil) => switch (perfil) {
        'admin' => const InfoPerfil('Administrador', 'Acesso total, inclusive esta área', Icons.admin_panel_settings),
        'operador' => const InfoPerfil('Operador', 'Painel, entradas/saídas e manutenção', Icons.support_agent),
        _ => const InfoPerfil('Motorista', 'App do motorista', Icons.directions_car),
      };
}

class EtiquetaPerfil extends StatelessWidget {
  const EtiquetaPerfil({super.key, required this.perfil, this.ativo = true});

  final String perfil;
  final bool ativo;

  @override
  Widget build(BuildContext context) {
    final info = InfoPerfil.de(perfil);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: ativo ? CoresPkLots.azulMarinho.withValues(alpha: 0.08) : CoresPkLots.borda,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(info.icone, size: 14, color: CoresPkLots.azulMarinho),
        const SizedBox(width: 4),
        Text(info.rotulo, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
      ]),
    );
  }
}
