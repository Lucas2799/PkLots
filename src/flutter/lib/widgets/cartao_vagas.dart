import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';
import '../util/formatadores.dart';

/// Número grande de vagas disponíveis + barra de disponibilidade
class CartaoVagas extends StatelessWidget {
  const CartaoVagas({super.key, required this.resumo, this.mostrarDetalhes = false});

  final ResumoVagas resumo;
  final bool mostrarDetalhes;

  @override
  Widget build(BuildContext context) {
    final percentual = (resumo.percentualDisponivel * 100).round();
    final lotado = resumo.disponiveis == 0;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(children: [
              Icon(Icons.local_parking, color: CoresPkLots.azulMarinho),
              SizedBox(width: 8),
              Text('Vagas disponíveis',
                  style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.textoSecundario)),
            ]),
            const SizedBox(height: 12),
            Semantics(
              label: '${resumo.disponiveis} de ${resumo.total} vagas disponíveis',
              excludeSemantics: true,
              child: Text.rich(TextSpan(children: [
                TextSpan(
                  text: '${resumo.disponiveis}',
                  style: const TextStyle(fontSize: 48, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho),
                ),
                TextSpan(
                  text: ' / ${resumo.total}',
                  style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: CoresPkLots.textoSuave),
                ),
              ])),
            ),
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: LinearProgressIndicator(
                value: resumo.percentualDisponivel,
                minHeight: 10,
                backgroundColor: CoresPkLots.borda,
                color: lotado ? CoresPkLots.erro : CoresPkLots.ambar,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              lotado ? 'Pátio lotado no momento' : '$percentual% disponível',
              style: TextStyle(
                color: lotado ? CoresPkLots.erro : CoresPkLots.textoSecundario,
                fontWeight: lotado ? FontWeight.w600 : FontWeight.normal,
              ),
            ),
            if (mostrarDetalhes) ...[
              const Divider(height: 28),
              Wrap(spacing: 20, runSpacing: 8, children: [
                _Contador(Icons.directions_car, 'Ocupadas', resumo.ocupadas, CoresPkLots.ocupada),
                _Contador(Icons.build_outlined, 'Manutenção', resumo.manutencao, CoresPkLots.manutencao),
              ]),
            ],
            const SizedBox(height: 4),
            Text('Atualizado às ${Formatadores.hora(resumo.atualizadoEm)}',
                style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
          ],
        ),
      ),
    );
  }
}

class _Contador extends StatelessWidget {
  const _Contador(this.icone, this.rotulo, this.valor, this.cor);

  final IconData icone;
  final String rotulo;
  final int valor;
  final Color cor;

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(icone, size: 18, color: cor),
        const SizedBox(width: 6),
        Text('$rotulo: ', style: const TextStyle(color: CoresPkLots.textoSecundario)),
        Text('$valor', style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
      ]);
}
