import 'package:flutter/material.dart';

import '../tema/cores.dart';

/// Logo completa (pino + "PkLots" + "Gestão de Estacionamentos")
class LogoPkLots extends StatelessWidget {
  const LogoPkLots({super.key, this.tamanho = 220});

  final double tamanho;

  @override
  Widget build(BuildContext context) => Semantics(
        label: 'PkLots - Gestão de Estacionamentos',
        image: true,
        child: Image.asset('assets/imagens/logo.webp', width: tamanho, height: tamanho, fit: BoxFit.contain),
      );
}

/// Marca compacta para barras de título: ícone do pino + "Pk" azul + "Lots" âmbar
class MarcaPkLots extends StatelessWidget {
  const MarcaPkLots({super.key, this.altura = 32});

  final double altura;

  @override
  Widget build(BuildContext context) {
    final estilo = TextStyle(fontSize: altura * 0.72, fontWeight: FontWeight.w800, letterSpacing: -0.5);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Image.asset('assets/imagens/icone.png', height: altura),
        const SizedBox(width: 8),
        Text.rich(
          TextSpan(children: [
            TextSpan(text: 'Pk', style: estilo.copyWith(color: CoresPkLots.azulMarinho)),
            TextSpan(text: 'Lots', style: estilo.copyWith(color: CoresPkLots.ambar)),
          ]),
          semanticsLabel: 'PkLots',
        ),
      ],
    );
  }
}
