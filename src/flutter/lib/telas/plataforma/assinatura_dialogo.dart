import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/caixa_dialogos.dart';

/// Plano, preço negociado, vencimento e período de teste de uma empresa (superadmin)
class AssinaturaDialogo extends StatefulWidget {
  const AssinaturaDialogo({super.key, required this.empresa});

  final EmpresaPlataforma empresa;

  @override
  State<AssinaturaDialogo> createState() => _AssinaturaDialogoState();
}

class _AssinaturaDialogoState extends State<AssinaturaDialogo> {
  final _formulario = GlobalKey<FormState>();
  final _preco = TextEditingController();
  final _dia = TextEditingController();
  final _observacoes = TextEditingController();
  List<Plano>? _planos;
  AssinaturaEmpresa? _assinatura;
  String? _planoId;
  DateTime? _testeAte;
  bool _salvando = false;
  String? _erro;

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  @override
  void dispose() {
    for (final c in [_preco, _dia, _observacoes]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final resultados = await Future.wait([_api.planos(), _api.assinaturaEmpresa(widget.empresa.id)]);
      final assinatura = resultados[1] as AssinaturaEmpresa;
      if (!mounted) return;
      setState(() {
        _planos = resultados[0] as List<Plano>;
        _assinatura = assinatura;
        _planoId = assinatura.planoId;
        _testeAte = assinatura.testeAte;
        _preco.text = assinatura.precoPorPatioNegociado?.toStringAsFixed(2).replaceAll('.', ',') ?? '';
        _dia.text = '${assinatura.diaVencimento}';
        _observacoes.text = assinatura.observacoes ?? '';
      });
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  Plano? get _plano => _planos?.where((p) => p.id == _planoId).firstOrNull;

  double get _precoEfetivo => lerReais(_preco.text) ?? _plano?.precoPorPatio ?? 0;

  Future<void> _escolherTeste() async {
    final agora = DateTime.now();
    final data = await showDatePicker(
      context: context,
      firstDate: DateTime(agora.year, agora.month, agora.day),
      lastDate: DateTime(agora.year + 1, agora.month, agora.day),
      initialDate: _testeAte ?? DateTime(agora.year, agora.month, agora.day + 30),
      helpText: 'Teste grátis até',
    );
    if (data != null) setState(() => _testeAte = data);
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _salvando = true;
      _erro = null;
    });
    try {
      await _api.salvarAssinatura(
        widget.empresa.id,
        planoId: _planoId!,
        precoNegociado: _preco.text.trim().isEmpty ? null : lerReais(_preco.text),
        diaVencimento: int.parse(_dia.text),
        testeAte: _testeAte,
        observacoes: _observacoes.text.trim().isEmpty ? null : _observacoes.text.trim(),
      );
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final planos = _planos;
    final assinatura = _assinatura;

    return AlertDialog(
      title: Text('Assinatura · ${widget.empresa.nome}'),
      content: SizedBox(
        width: 440,
        child: planos == null || assinatura == null
            ? Padding(
                padding: const EdgeInsets.all(24),
                child: _erro == null
                    ? const Center(child: CircularProgressIndicator())
                    : Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
              )
            : Form(
                key: _formulario,
                child: SingleChildScrollView(
                  child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    DropdownButtonFormField<String>(
                      initialValue: _planoId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Plano'),
                      items: [
                        for (final p in planos)
                          if (p.ativo || p.id == assinatura.planoId)
                            DropdownMenuItem(
                              value: p.id,
                              child: Text('${p.nome} · ${Formatadores.moeda(p.precoPorPatio)}/pátio${p.ativo ? '' : ' (inativo)'}'),
                            ),
                      ],
                      onChanged: (id) => setState(() => _planoId = id),
                    ),
                    const SizedBox(height: 12),
                    Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Expanded(
                        child: TextFormField(
                          controller: _preco,
                          decoration: const InputDecoration(
                              labelText: 'Preço negociado/pátio', prefixText: 'R\$ ', hintText: 'Do plano'),
                          keyboardType: const TextInputType.numberWithOptions(decimal: true),
                          onChanged: (_) => setState(() {}),
                          validator: (v) => v == null || v.trim().isEmpty || (lerReais(v) ?? -1) >= 0 ? null : 'Valor inválido',
                        ),
                      ),
                      const SizedBox(width: 12),
                      SizedBox(
                        width: 130,
                        child: TextFormField(
                          controller: _dia,
                          decoration: const InputDecoration(labelText: 'Dia do vencimento'),
                          keyboardType: TextInputType.number,
                          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                          validator: (v) {
                            final n = int.tryParse(v ?? '');
                            return n == null || n < 1 || n > 28 ? '1 a 28' : null;
                          },
                        ),
                      ),
                    ]),
                    const SizedBox(height: 12),
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: const Icon(Icons.card_giftcard_outlined),
                      title: Text(_testeAte == null ? 'Sem período de teste' : 'Teste grátis até ${Formatadores.data(_testeAte!)}'),
                      subtitle: const Text('Sem cobrança até essa data'),
                      trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                        if (_testeAte != null)
                          IconButton(
                              tooltip: 'Remover teste', icon: const Icon(Icons.close), onPressed: () => setState(() => _testeAte = null)),
                        IconButton(tooltip: 'Escolher data', icon: const Icon(Icons.edit_calendar_outlined), onPressed: _escolherTeste),
                      ]),
                    ),
                    TextFormField(
                      controller: _observacoes,
                      decoration: const InputDecoration(labelText: 'Observações (opcional)'),
                      maxLength: 500,
                    ),
                    Card(
                      color: CoresPkLots.fundo,
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Text(
                          '${assinatura.patiosAtivos} pátio(s) ativo(s) × ${Formatadores.moeda(_precoEfetivo)} = '
                          '${Formatadores.moeda(_precoEfetivo * assinatura.patiosAtivos)} por mês',
                          style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho),
                        ),
                      ),
                    ),
                    if (_erro != null) ...[
                      const SizedBox(height: 8),
                      Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                    ],
                  ]),
                ),
              ),
      ),
      actions: [
        TextButton(onPressed: _salvando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
        SizedBox(
          width: 120,
          child: FilledButton(
            onPressed: _salvando || assinatura == null ? null : _salvar,
            child: _salvando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Salvar'),
          ),
        ),
      ],
    );
  }
}
