import 'package:flutter/material.dart';

import 'logo_pklots.dart';

/// Moldura das telas de login/cadastro: logo em cima e formulário centralizado
class LayoutAutenticacao extends StatelessWidget {
  const LayoutAutenticacao({super.key, required this.child, this.tamanhoLogo = 200});

  final Widget child;
  final double tamanhoLogo;

  @override
  Widget build(BuildContext context) => Scaffold(
        // Branco para combinar com o fundo da imagem da logo
        backgroundColor: Colors.white,
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Column(children: [
                  LogoPkLots(tamanho: tamanhoLogo),
                  const SizedBox(height: 8),
                  Card(child: Padding(padding: const EdgeInsets.all(24), child: child)),
                ]),
              ),
            ),
          ),
        ),
      );
}
