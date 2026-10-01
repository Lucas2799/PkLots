import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../tema/cores.dart';

/// Leitura do QR Code do ticket pela câmera (Android/iOS). Retorna o texto lido.
class LeitorTicketTela extends StatefulWidget {
  const LeitorTicketTela({super.key});

  @override
  State<LeitorTicketTela> createState() => _LeitorTicketTelaState();
}

class _LeitorTicketTelaState extends State<LeitorTicketTela> {
  final _controle = MobileScannerController(formats: const [BarcodeFormat.qrCode]);
  bool _lido = false;

  @override
  void dispose() {
    _controle.dispose();
    super.dispose();
  }

  void _aoDetectar(BarcodeCapture captura) {
    if (_lido) return;
    final valor = captura.barcodes.map((c) => c.rawValue).whereType<String>().firstOrNull;
    if (valor == null) return;
    _lido = true;
    Navigator.of(context).pop(valor);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: Colors.black,
        appBar: AppBar(
          title: const Text('Ler ticket'),
          actions: [
            IconButton(
              tooltip: 'Lanterna',
              icon: const Icon(Icons.flashlight_on_outlined),
              onPressed: _controle.toggleTorch,
            ),
          ],
        ),
        body: Stack(children: [
          MobileScanner(
            controller: _controle,
            onDetect: _aoDetectar,
            errorBuilder: (context, erro) => Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  'Não foi possível abrir a câmera. Verifique a permissão do app.\n(${erro.errorCode.name})',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white),
                ),
              ),
            ),
          ),
          // Moldura de mira
          Center(
            child: Container(
              width: 240,
              height: 240,
              decoration: BoxDecoration(
                border: Border.all(color: CoresPkLots.ambar, width: 3),
                borderRadius: BorderRadius.circular(16),
              ),
            ),
          ),
          const Positioned(
            left: 0,
            right: 0,
            bottom: 48,
            child: Text('Aponte para o QR Code do ticket',
                textAlign: TextAlign.center, style: TextStyle(color: Colors.white, fontSize: 16)),
          ),
        ]),
      );
}
