import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/motorista_provider.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/validadores.dart';

/// Vincula uma placa ao motorista. Retorna true quando cadastrou.
Future<bool?> mostrarCadastroVeiculo(BuildContext context) =>
    showDialog<bool>(context: context, builder: (_) => const _CadastroVeiculoDialogo());

class _CadastroVeiculoDialogo extends StatefulWidget {
  const _CadastroVeiculoDialogo();

  @override
  State<_CadastroVeiculoDialogo> createState() => _CadastroVeiculoDialogoState();
}

class _CadastroVeiculoDialogoState extends State<_CadastroVeiculoDialogo> {
  final _formulario = GlobalKey<FormState>();
  final _placa = TextEditingController();
  final _modelo = TextEditingController();
  final _cor = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    for (final c in [_placa, _modelo, _cor]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<MotoristaProvider>().cadastrarVeiculo(
            Validadores.normalizarPlaca(_placa.text),
            modelo: _modelo.text.trim().isEmpty ? null : _modelo.text.trim(),
            cor: _cor.text.trim().isEmpty ? null : _cor.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Cadastrar veículo'),
        content: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 360),
          child: Form(
            key: _formulario,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              TextFormField(
                controller: _placa,
                autofocus: true,
                decoration: const InputDecoration(labelText: 'Placa', hintText: 'ABC1234 ou ABC1D23'),
                textCapitalization: TextCapitalization.characters,
                validator: Validadores.placa,
              ),
              const SizedBox(height: 12),
              TextFormField(controller: _modelo, decoration: const InputDecoration(labelText: 'Modelo (opcional)')),
              const SizedBox(height: 12),
              TextFormField(
                controller: _cor,
                decoration: const InputDecoration(labelText: 'Cor (opcional)'),
                onFieldSubmitted: (_) => _salvar(),
              ),
              if (_erro != null) ...[
                const SizedBox(height: 12),
                Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
              ],
            ]),
          ),
        ),
        actions: [
          TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
          SizedBox(
            width: 120,
            child: FilledButton(
              onPressed: _enviando ? null : _salvar,
              child: _enviando
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Salvar'),
            ),
          ),
        ],
      );
}
