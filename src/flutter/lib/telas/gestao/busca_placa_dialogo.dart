import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/validadores.dart';
import '../../widgets/item_historico_tile.dart';

/// Histórico de entradas e saídas de uma placa
Future<void> mostrarBuscaPlaca(BuildContext context) =>
    showDialog(context: context, builder: (_) => const _BuscaPlacaDialogo());

class _BuscaPlacaDialogo extends StatefulWidget {
  const _BuscaPlacaDialogo();

  @override
  State<_BuscaPlacaDialogo> createState() => _BuscaPlacaDialogoState();
}

class _BuscaPlacaDialogoState extends State<_BuscaPlacaDialogo> {
  final _placa = TextEditingController();
  List<ItemHistorico>? _resultado;
  String? _erro;
  bool _buscando = false;

  @override
  void dispose() {
    _placa.dispose();
    super.dispose();
  }

  Future<void> _buscar() async {
    final invalida = Validadores.placa(_placa.text);
    if (invalida != null) {
      setState(() => _erro = invalida);
      return;
    }
    setState(() {
      _buscando = true;
      _erro = null;
    });
    try {
      final resultado =
          await context.read<GestaoProvider>().historicoVeiculo(Validadores.normalizarPlaca(_placa.text));
      setState(() => _resultado = resultado);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _buscando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Histórico por placa'),
        content: SizedBox(
          width: 460,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(
              controller: _placa,
              autofocus: true,
              textCapitalization: TextCapitalization.characters,
              decoration: InputDecoration(
                labelText: 'Placa',
                errorText: _erro,
                suffixIcon: IconButton(
                  tooltip: 'Buscar',
                  icon: _buscando
                      ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.search),
                  onPressed: _buscando ? null : _buscar,
                ),
              ),
              onSubmitted: (_) => _buscar(),
            ),
            const SizedBox(height: 12),
            if (_resultado != null)
              _resultado!.isEmpty
                  ? const Padding(
                      padding: EdgeInsets.all(16),
                      child: Text('Nenhum registro para esta placa.', style: TextStyle(color: CoresPkLots.textoSecundario)),
                    )
                  : Flexible(
                      child: ListView(
                        shrinkWrap: true,
                        children: [for (final item in _resultado!) ItemHistoricoTile(item: item, mostrarPlaca: false)],
                      ),
                    ),
          ]),
        ),
        actions: [TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Fechar'))],
      );
}
