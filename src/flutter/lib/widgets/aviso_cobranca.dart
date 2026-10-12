import 'package:flutter/material.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';
import '../util/formatadores.dart';

/// Faixa no topo do painel: fatura do PkLots vencida (admin) ou bloqueio parcial (todos)
class AvisoCobranca extends StatelessWidget {
  const AvisoCobranca({super.key, required this.cobranca, required this.ehAdmin});

  final SituacaoCobranca? cobranca;
  final bool ehAdmin;

  @override
  Widget build(BuildContext context) {
    final c = cobranca;
    if (c == null || c.situacao == SituacaoCobranca.emDia) return const SizedBox.shrink();
    final bloqueada = c.situacao == SituacaoCobranca.bloqueada;
    if (!bloqueada && !ehAdmin) return const SizedBox.shrink();

    final cor = bloqueada ? CoresPkLots.erro : CoresPkLots.manutencao;
    final texto = bloqueada
        ? 'Acesso parcialmente bloqueado: a fatura do PkLots está vencida há ${c.diasEmAtraso} dias. '
            'Novas entradas${ehAdmin ? ' e mudanças na administração' : ''} voltam após o pagamento; saídas e caixa continuam funcionando.'
        : 'Fatura do PkLots vencida há ${c.diasEmAtraso} dia(s) (${Formatadores.moeda(c.valorVencido)}). '
            'Se não for paga, novas entradas serão bloqueadas em ${c.diasParaBloqueio} dia(s).';

    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Card(
        color: cor.withValues(alpha: 0.08),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(children: [
            Icon(bloqueada ? Icons.lock_outline : Icons.warning_amber_rounded, color: cor),
            const SizedBox(width: 12),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(texto, style: TextStyle(color: cor, fontWeight: FontWeight.w600)),
                if (ehAdmin)
                  const Padding(
                    padding: EdgeInsets.only(top: 4),
                    child: Text('Veja a fatura e o PIX em Administração > Assinatura.', style: TextStyle(fontSize: 13)),
                  )
                else
                  const Padding(
                    padding: EdgeInsets.only(top: 4),
                    child: Text('Avise o administrador da empresa.', style: TextStyle(fontSize: 13)),
                  ),
              ]),
            ),
          ]),
        ),
      ),
    );
  }
}
