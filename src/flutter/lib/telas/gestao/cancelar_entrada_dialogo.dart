import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';

/// Mesmo prazo da API: depois disso só o admin cancela
const minutosCancelamentoOperador = 10;

const _motivosComuns = ['Placa digitada errada', 'Entrada em duplicidade', 'Saiu sem passar pelo sistema'];

/// Cancela a entrada de um veículo estacionado (com motivo). Retorna true se cancelou.
Future<bool?> mostrarCancelarEntrada(BuildContext context, VeiculoEstacionado veiculo) => showDialog<bool>(
  context: context,
  builder: (_) => _CancelarEntradaDialogo(veiculo: veiculo),
);

class _CancelarEntradaDialogo extends StatefulWidget {
  const _CancelarEntradaDialogo({required this.veiculo});

  final VeiculoEstacionado veiculo;

  @override
  State<_CancelarEntradaDialogo> createState() => _CancelarEntradaDialogoState();
}

class _CancelarEntradaDialogoState extends State<_CancelarEntradaDialogo> {
  final _motivo = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _motivo.dispose();
    super.dispose();
  }

  Future<void> _confirmar() async {
    final motivo = _motivo.text.trim();
    if (motivo.isEmpty) {
      setState(() => _erro = 'Informe o motivo do cancelamento.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<GestaoProvider>().cancelarEntrada(widget.veiculo.entradaId, motivo);
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final ehAdmin = context.read<SessaoProvider>().ehAdmin;
    return AlertDialog(
      title: Text('Cancelar entrada de ${Formatadores.placa(widget.veiculo.placa)}'),
      content: SizedBox(
        width: 380,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'A vaga é liberada sem registrar saída nem cobrança. O cancelamento fica registrado com o motivo.',
              style: TextStyle(color: CoresPkLots.textoSecundario),
            ),
            if (!ehAdmin) ...[
              const SizedBox(height: 8),
              const Text(
                'Operador: até $minutosCancelamentoOperador minutos após a entrada. Depois disso, peça ao administrador.',
                style: TextStyle(color: CoresPkLots.ambarEscuro),
              ),
            ],
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final motivo in _motivosComuns)
                  ActionChip(
                    label: Text(motivo),
                    onPressed: _enviando ? null : () => setState(() => _motivo.text = motivo),
                  ),
              ],
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _motivo,
              autofocus: true,
              maxLength: 200,
              decoration: const InputDecoration(labelText: 'Motivo'),
              onSubmitted: (_) => _confirmar(),
            ),
            if (_erro != null) ...[
              const SizedBox(height: 4),
              Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(false), child: const Text('Voltar')),
        SizedBox(
          width: 170,
          child: FilledButton(
            style: FilledButton.styleFrom(backgroundColor: CoresPkLots.erro),
            onPressed: _enviando ? null : _confirmar,
            child: _enviando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Cancelar entrada'),
          ),
        ),
      ],
    );
  }
}
