import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../util/validadores.dart';

/// Cadastro/edição de mensalista. Retorna o mensalista salvo.
Future<Mensalista?> mostrarFormularioMensalista(BuildContext context, {Mensalista? mensalista}) =>
    showDialog<Mensalista>(context: context, builder: (_) => _FormularioMensalista(mensalista: mensalista));

class _FormularioMensalista extends StatefulWidget {
  const _FormularioMensalista({this.mensalista});

  final Mensalista? mensalista;

  @override
  State<_FormularioMensalista> createState() => _FormularioMensalistaState();
}

class _FormularioMensalistaState extends State<_FormularioMensalista> {
  final _formulario = GlobalKey<FormState>();
  late final _nome = TextEditingController(text: widget.mensalista?.nome);
  late final _telefone = TextEditingController(text: widget.mensalista?.telefone);
  late final _documento = TextEditingController(text: widget.mensalista?.documento);
  late final _observacoes = TextEditingController(text: widget.mensalista?.observacoes);
  final _novaPlaca = TextEditingController();
  late final List<String> _placas = [...?widget.mensalista?.placas];
  late bool _ativo = widget.mensalista?.ativo ?? true;
  String? _erroPlaca;
  String? _erro;
  bool _enviando = false;

  bool get _editando => widget.mensalista != null;

  @override
  void dispose() {
    for (final c in [_nome, _telefone, _documento, _observacoes, _novaPlaca]) {
      c.dispose();
    }
    super.dispose();
  }

  void _adicionarPlaca() {
    final invalida = Validadores.placa(_novaPlaca.text);
    if (invalida != null) {
      setState(() => _erroPlaca = invalida);
      return;
    }
    final placa = Validadores.normalizarPlaca(_novaPlaca.text);
    setState(() {
      if (!_placas.contains(placa)) _placas.add(placa);
      _novaPlaca.clear();
      _erroPlaca = null;
    });
  }

  Future<void> _salvar() async {
    if (_novaPlaca.text.trim().isNotEmpty) _adicionarPlaca();
    if (!_formulario.currentState!.validate()) return;
    if (_placas.isEmpty) {
      setState(() => _erroPlaca = 'Adicione pelo menos uma placa');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    String? texto(TextEditingController c) => c.text.trim().isEmpty ? null : c.text.trim();
    try {
      final salvo = await context.read<ClienteApi>().salvarMensalista(
            id: widget.mensalista?.id,
            nome: _nome.text.trim(),
            telefone: texto(_telefone),
            documento: texto(_documento),
            observacoes: texto(_observacoes),
            placas: _placas,
            ativo: _ativo,
          );
      if (mounted) Navigator.of(context).pop(salvo);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text(_editando ? 'Editar mensalista' : 'Novo mensalista'),
        content: SizedBox(
          width: 420,
          child: Form(
            key: _formulario,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                TextFormField(
                  controller: _nome,
                  autofocus: !_editando,
                  textCapitalization: TextCapitalization.words,
                  decoration: const InputDecoration(labelText: 'Nome'),
                  validator: (v) => Validadores.obrigatorio(v, 'o nome'),
                ),
                const SizedBox(height: 12),
                Row(children: [
                  Expanded(
                    child: TextFormField(
                      controller: _telefone,
                      keyboardType: TextInputType.phone,
                      decoration: const InputDecoration(labelText: 'Telefone (opcional)'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(controller: _documento, decoration: const InputDecoration(labelText: 'CPF/CNPJ (opcional)')),
                  ),
                ]),
                const SizedBox(height: 16),
                const Text('Placas', style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                const SizedBox(height: 8),
                Wrap(spacing: 8, runSpacing: 8, children: [
                  for (final placa in _placas)
                    InputChip(
                      avatar: const Icon(Icons.directions_car, size: 18),
                      label: Text(Formatadores.placa(placa)),
                      onDeleted: () => setState(() => _placas.remove(placa)),
                      deleteButtonTooltipMessage: 'Remover $placa',
                    ),
                ]),
                const SizedBox(height: 8),
                TextField(
                  controller: _novaPlaca,
                  textCapitalization: TextCapitalization.characters,
                  decoration: InputDecoration(
                    labelText: 'Adicionar placa',
                    hintText: 'ABC1234 ou ABC1D23',
                    errorText: _erroPlaca,
                    suffixIcon: IconButton(tooltip: 'Adicionar placa', icon: const Icon(Icons.add), onPressed: _adicionarPlaca),
                  ),
                  onSubmitted: (_) => _adicionarPlaca(),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _observacoes,
                  maxLines: 2,
                  decoration: const InputDecoration(labelText: 'Observações (opcional)'),
                ),
                if (_editando)
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Mensalista ativo'),
                    subtitle: const Text('Inativo é cobrado como avulso e libera as placas para outro cadastro'),
                    value: _ativo,
                    onChanged: (v) => setState(() => _ativo = v),
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
          TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(), child: const Text('Cancelar')),
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
