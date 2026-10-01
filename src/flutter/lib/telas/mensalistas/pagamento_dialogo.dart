import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';

/// Registra pagamento de mensalidade. Retorna o mensalista com a nova validade.
Future<Mensalista?> mostrarRegistroPagamento(BuildContext context, Mensalista mensalista, double valorMensalidade) =>
    showDialog<Mensalista>(
        context: context, builder: (_) => _PagamentoDialogo(mensalista: mensalista, valorMensalidade: valorMensalidade));

class _PagamentoDialogo extends StatefulWidget {
  const _PagamentoDialogo({required this.mensalista, required this.valorMensalidade});

  final Mensalista mensalista;
  final double valorMensalidade;

  @override
  State<_PagamentoDialogo> createState() => _PagamentoDialogoState();
}

class _PagamentoDialogoState extends State<_PagamentoDialogo> {
  int _meses = 1;
  String _forma = 'pix';
  late final _valor = TextEditingController(text: _formatar(widget.valorMensalidade));
  final _observacao = TextEditingController();
  bool _valorEditado = false;
  bool _enviando = false;
  String? _erro;

  static String _formatar(double v) => v.toStringAsFixed(2).replaceAll('.', ',');

  static double? _ler(String texto) {
    final limpo = texto.trim();
    return double.tryParse(limpo.contains(',') ? limpo.replaceAll('.', '').replaceAll(',', '.') : limpo);
  }

  @override
  void dispose() {
    _valor.dispose();
    _observacao.dispose();
    super.dispose();
  }

  /// Mesma regra da API: em dia continua de onde parou; vencido recomeça hoje
  (DateTime, DateTime) get _periodo {
    final hoje = DateUtils.dateOnly(DateTime.now());
    final ate = widget.mensalista.validoAte;
    final de = ate != null && !ate.isBefore(hoje) ? ate.add(const Duration(days: 1)) : hoje;
    final fim = DateTime(de.year, de.month + _meses, de.day).subtract(const Duration(days: 1));
    return (de, fim);
  }

  void _alterarMeses(int meses) {
    setState(() {
      _meses = meses;
      if (!_valorEditado) _valor.text = _formatar(widget.valorMensalidade * meses);
    });
  }

  Future<void> _registrar() async {
    final valor = _ler(_valor.text);
    if (valor == null || valor <= 0) {
      setState(() => _erro = 'Informe o valor pago.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      final atualizado = await context.read<ClienteApi>().registrarPagamento(
            widget.mensalista.id,
            meses: _meses,
            valor: valor,
            forma: _forma,
            observacao: _observacao.text.trim().isEmpty ? null : _observacao.text.trim(),
          );
      if (mounted) Navigator.of(context).pop(atualizado);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final (de, ate) = _periodo;
    return AlertDialog(
      title: Text('Pagamento · ${widget.mensalista.nome}'),
      content: SizedBox(
        width: 400,
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          DropdownButtonFormField<int>(
            initialValue: _meses,
            decoration: const InputDecoration(labelText: 'Meses'),
            items: [for (var m = 1; m <= 12; m++) DropdownMenuItem(value: m, child: Text('$m ${m == 1 ? 'mês' : 'meses'}'))],
            onChanged: (m) => _alterarMeses(m ?? 1),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _valor,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[\d.,]'))],
            decoration: InputDecoration(
              labelText: 'Valor pago',
              prefixText: 'R\$ ',
              helperText: widget.valorMensalidade > 0
                  ? 'Mensalidade do pátio: ${Formatadores.moeda(widget.valorMensalidade)}'
                  : 'Mensalidade do pátio não configurada',
            ),
            onChanged: (_) => _valorEditado = true,
          ),
          const SizedBox(height: 12),
          const Text('Forma de pagamento', style: TextStyle(color: CoresPkLots.textoSecundario)),
          const SizedBox(height: 6),
          Wrap(spacing: 8, children: [
            for (final MapEntry(key: forma, value: rotulo) in PagamentoMensalidade.formas.entries)
              ChoiceChip(label: Text(rotulo), selected: _forma == forma, onSelected: (_) => setState(() => _forma = forma)),
          ]),
          const SizedBox(height: 12),
          TextField(controller: _observacao, decoration: const InputDecoration(labelText: 'Observação (opcional)')),
          const SizedBox(height: 14),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(color: CoresPkLots.fundo, borderRadius: BorderRadius.circular(10)),
            child: Row(children: [
              const Icon(Icons.event_available, color: CoresPkLots.disponivel),
              const SizedBox(width: 10),
              Expanded(
                child: Text('Válido de ${Formatadores.data(de)} até ${Formatadores.data(ate)}',
                    style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
              ),
            ]),
          ),
          if (_erro != null) ...[
            const SizedBox(height: 10),
            Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
          ],
        ]),
      ),
      actions: [
        TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        SizedBox(
          width: 140,
          child: FilledButton(
            onPressed: _enviando ? null : _registrar,
            child: _enviando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Registrar'),
          ),
        ),
      ],
    );
  }
}
