import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../estado/gestao_provider.dart';
import '../servicos/excecao_api.dart';
import '../tema/cores.dart';

/// Lê valores em reais: "15,50", "1.234,50" ou "15.50"
double? lerReais(String texto) {
  final limpo = texto.trim();
  return double.tryParse(limpo.contains(',') ? limpo.replaceAll('.', '').replaceAll(',', '.') : limpo);
}

String formatarReais(double valor) => valor.toStringAsFixed(2).replaceAll('.', ',');

final filtroReais = FilteringTextInputFormatter.allow(RegExp(r'[\d.,]'));

/// Abre o caixa do usuário no pátio selecionado. Retorna true se abriu.
Future<bool?> mostrarAbrirCaixa(BuildContext context) =>
    showDialog<bool>(context: context, builder: (_) => const _DialogoValor(tipo: _TipoDialogo.abrir));

/// Sangria (retirada) ou suprimento (reforço de troco)
Future<bool?> mostrarMovimentoCaixa(BuildContext context, {required bool sangria}) => showDialog<bool>(
    context: context, builder: (_) => _DialogoValor(tipo: sangria ? _TipoDialogo.sangria : _TipoDialogo.suprimento));

enum _TipoDialogo { abrir, sangria, suprimento }

class _DialogoValor extends StatefulWidget {
  const _DialogoValor({required this.tipo});

  final _TipoDialogo tipo;

  @override
  State<_DialogoValor> createState() => _DialogoValorState();
}

class _DialogoValorState extends State<_DialogoValor> {
  final _valor = TextEditingController(text: '0,00');
  final _motivo = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _valor.dispose();
    _motivo.dispose();
    super.dispose();
  }

  (String, String, String) get _textos => switch (widget.tipo) {
        _TipoDialogo.abrir => ('Abrir caixa', 'Troco inicial', 'Dinheiro na gaveta ao começar o turno.'),
        _TipoDialogo.sangria => ('Sangria', 'Valor retirado', 'Dinheiro retirado do caixa (ex.: levado ao cofre).'),
        _TipoDialogo.suprimento => ('Suprimento', 'Valor colocado', 'Dinheiro colocado no caixa (ex.: reforço de troco).'),
      };

  Future<void> _confirmar() async {
    final valor = lerReais(_valor.text);
    if (valor == null || valor < 0 || (widget.tipo != _TipoDialogo.abrir && valor == 0)) {
      setState(() => _erro = 'Informe um valor válido.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    final estado = context.read<GestaoProvider>();
    final motivo = _motivo.text.trim().isEmpty ? null : _motivo.text.trim();
    try {
      switch (widget.tipo) {
        case _TipoDialogo.abrir:
          await estado.abrirCaixa(valor);
        case _TipoDialogo.sangria:
          await estado.movimentarCaixa('sangria', valor, motivo);
        case _TipoDialogo.suprimento:
          await estado.movimentarCaixa('suprimento', valor, motivo);
      }
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final (titulo, rotulo, ajuda) = _textos;
    return AlertDialog(
      title: Text(titulo),
      content: SizedBox(
        width: 360,
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text(ajuda, style: const TextStyle(color: CoresPkLots.textoSecundario)),
          const SizedBox(height: 12),
          TextField(
            controller: _valor,
            autofocus: true,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            inputFormatters: [filtroReais],
            decoration: InputDecoration(labelText: rotulo, prefixText: 'R\$ '),
            onSubmitted: (_) => _confirmar(),
          ),
          if (widget.tipo != _TipoDialogo.abrir) ...[
            const SizedBox(height: 12),
            TextField(controller: _motivo, decoration: const InputDecoration(labelText: 'Motivo (opcional)')),
          ],
          if (_erro != null) ...[
            const SizedBox(height: 10),
            Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
          ],
        ]),
      ),
      actions: [
        TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
        SizedBox(
          width: 130,
          child: FilledButton(
            onPressed: _enviando ? null : _confirmar,
            child: _enviando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Confirmar'),
          ),
        ),
      ],
    );
  }
}
