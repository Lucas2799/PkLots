import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../modelos/modelos.dart';
import '../tema/cores.dart';
import '../util/formatadores.dart';

/// Situação da fatura com ícone + texto (não só cor)
class SituacaoFatura extends StatelessWidget {
  const SituacaoFatura({super.key, required this.fatura});

  final Fatura fatura;

  @override
  Widget build(BuildContext context) {
    final (icone, texto, cor) = switch (fatura.situacao) {
      Fatura.paga => (Icons.check_circle, 'Paga', CoresPkLots.disponivel),
      Fatura.vencida => (Icons.error_outline, 'Vencida há ${fatura.diasEmAtraso} dia(s)', CoresPkLots.erro),
      Fatura.cancelada => (Icons.block, 'Cancelada', CoresPkLots.textoSuave),
      _ => (Icons.schedule, 'Aberta', CoresPkLots.manutencao),
    };
    return Row(mainAxisSize: MainAxisSize.min, children: [
      Icon(icone, size: 16, color: cor),
      const SizedBox(width: 4),
      Text(texto, style: TextStyle(color: cor, fontWeight: FontWeight.w600, fontSize: 13)),
    ]);
  }
}

/// QR Code e PIX copia e cola de uma fatura aberta
Future<void> mostrarPixFatura(BuildContext context, Fatura fatura) => showDialog<void>(
      context: context,
      builder: (contexto) => AlertDialog(
        title: const Text('Pagar com PIX'),
        content: SizedBox(
          width: 320,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(fatura.descricao, textAlign: TextAlign.center),
            const SizedBox(height: 4),
            Text(Formatadores.moeda(fatura.valor),
                style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
            Text('Vencimento ${Formatadores.data(fatura.vencimento)}', style: const TextStyle(color: CoresPkLots.textoSecundario)),
            const SizedBox(height: 12),
            Container(
              color: Colors.white,
              padding: const EdgeInsets.all(8),
              child: QrImageView(data: fatura.pixCopiaECola!, size: 220, semanticsLabel: 'QR Code PIX da fatura'),
            ),
            const SizedBox(height: 8),
            const Text('Depois do pagamento, a baixa é feita pela equipe do PkLots.',
                textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
          ]),
        ),
        actions: [
          TextButton.icon(
            onPressed: () async {
              await Clipboard.setData(ClipboardData(text: fatura.pixCopiaECola!));
              if (contexto.mounted) {
                ScaffoldMessenger.of(contexto).showSnackBar(const SnackBar(content: Text('PIX copia e cola copiado.')));
              }
            },
            icon: const Icon(Icons.copy),
            label: const Text('Copiar código'),
          ),
          TextButton(onPressed: () => Navigator.of(contexto).pop(), child: const Text('Fechar')),
        ],
      ),
    );
