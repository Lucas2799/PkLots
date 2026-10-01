import 'package:flutter/material.dart';

import '../tema/cores.dart';

/// Mensagem de erro com botão "Tentar novamente"
class MensagemErro extends StatelessWidget {
  const MensagemErro({super.key, required this.mensagem, this.aoTentarNovamente});

  final String mensagem;
  final VoidCallback? aoTentarNovamente;

  @override
  Widget build(BuildContext context) => Card(
        color: CoresPkLots.erro.withValues(alpha: 0.06),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(children: [
            const Icon(Icons.error_outline, color: CoresPkLots.erro),
            const SizedBox(width: 12),
            Expanded(child: Text(mensagem)),
            if (aoTentarNovamente != null)
              TextButton(onPressed: aoTentarNovamente, child: const Text('Tentar novamente')),
          ]),
        ),
      );
}

/// Título de seção dentro das telas
class TituloSecao extends StatelessWidget {
  const TituloSecao(this.texto, {super.key, this.icone, this.acao});

  final String texto;
  final IconData? icone;
  final Widget? acao;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Row(children: [
          if (icone != null) ...[Icon(icone, size: 20, color: CoresPkLots.azulMarinho), const SizedBox(width: 8)],
          Expanded(
            child: Text(texto,
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
          ),
          ?acao,
        ]),
      );
}

/// Mostra uma mensagem rápida no rodapé
void mostrarAviso(BuildContext context, String mensagem, {bool erro = false, SnackBarAction? acao}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(mensagem),
      backgroundColor: erro ? CoresPkLots.erro : CoresPkLots.azulMarinho,
      action: acao,
      duration: Duration(seconds: acao == null ? 4 : 8),
    ));
}
