import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';

/// Pátios da empresa: criar, renomear, ativar/desativar
class AbaPatios extends StatelessWidget {
  const AbaPatios({super.key});

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<AdministracaoProvider>();

    return ListView(padding: const EdgeInsets.all(16), children: [
      Row(children: [
        Expanded(
          child: Text('${estado.patios.where((p) => p.ativo).length} pátio(s) ativo(s)',
              style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho, fontSize: 16)),
        ),
        SizedBox(
          width: 170,
          child: FilledButton.icon(
            onPressed: () => showDialog(context: context, builder: (_) => const _NovoPatio()),
            icon: const Icon(Icons.add_business_outlined),
            label: const Text('Novo pátio'),
          ),
        ),
      ]),
      const SizedBox(height: 6),
      const Text('Preço, vagas e impressão são configurados por pátio: escolha o pátio no topo do painel.',
          style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
      const SizedBox(height: 12),
      Card(
        child: Column(children: [
          for (final (i, patio) in estado.patios.indexed) ...[
            if (i > 0) const Divider(height: 1),
            ListTile(
              leading: CircleAvatar(
                backgroundColor: patio.ativo ? CoresPkLots.azulMarinho : CoresPkLots.borda,
                foregroundColor: patio.ativo ? Colors.white : CoresPkLots.textoSuave,
                child: const Icon(Icons.local_parking),
              ),
              title: Text(patio.nome,
                  style: TextStyle(
                    fontWeight: FontWeight.w600,
                    color: patio.ativo ? CoresPkLots.azulMarinho : CoresPkLots.textoSuave,
                  )),
              subtitle: Text([
                '${patio.totalVagas} vagas',
                '${Formatadores.moeda(patio.valorHora)}/h',
                '${patio.veiculosEstacionados} estacionado(s)',
                if (!patio.ativo) 'Desativado',
              ].join(' · ')),
              trailing: const Icon(Icons.edit_outlined, size: 20),
              onTap: () => showDialog(context: context, builder: (_) => _EditarPatio(patio: patio)),
            ),
          ],
        ]),
      ),
    ]);
  }
}

class _NovoPatio extends StatefulWidget {
  const _NovoPatio();

  @override
  State<_NovoPatio> createState() => _NovoPatioState();
}

class _NovoPatioState extends State<_NovoPatio> {
  final _formulario = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _vagas = TextEditingController(text: '30');
  final _valor = TextEditingController(text: '15,00');
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    for (final c in [_nome, _vagas, _valor]) {
      c.dispose();
    }
    super.dispose();
  }

  static double? _lerValor(String texto) {
    final limpo = texto.trim();
    return double.tryParse(limpo.contains(',') ? limpo.replaceAll('.', '').replaceAll(',', '.') : limpo);
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<AdministracaoProvider>().criarPatio(_nome.text.trim(), int.parse(_vagas.text), _lerValor(_valor.text)!);
      if (mounted) {
        Navigator.of(context).pop();
        mostrarAviso(context, 'Pátio criado. Selecione-o no topo do painel para configurar.');
      }
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Novo pátio'),
        content: SizedBox(
          width: 380,
          child: Form(
            key: _formulario,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              TextFormField(
                controller: _nome,
                autofocus: true,
                decoration: const InputDecoration(labelText: 'Nome', hintText: 'Ex.: Pátio Shopping'),
                validator: (v) => v == null || v.trim().isEmpty ? 'Informe o nome' : null,
              ),
              const SizedBox(height: 12),
              Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Expanded(
                  child: TextFormField(
                    controller: _vagas,
                    decoration: const InputDecoration(labelText: 'Vagas'),
                    keyboardType: TextInputType.number,
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                    validator: (v) {
                      final n = int.tryParse(v ?? '');
                      return n == null || n < 1 || n > 260 ? 'Entre 1 e 260' : null;
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _valor,
                    decoration: const InputDecoration(labelText: 'Valor/hora', prefixText: 'R\$ '),
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    validator: (v) {
                      final valor = _lerValor(v ?? '');
                      return valor == null || valor <= 0 ? 'Inválido' : null;
                    },
                  ),
                ),
              ]),
              if (_erro != null) ...[
                const SizedBox(height: 12),
                Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
              ],
            ]),
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
                  : const Text('Criar'),
            ),
          ),
        ],
      );
}

class _EditarPatio extends StatefulWidget {
  const _EditarPatio({required this.patio});

  final PatioAdmin patio;

  @override
  State<_EditarPatio> createState() => _EditarPatioState();
}

class _EditarPatioState extends State<_EditarPatio> {
  late final _nome = TextEditingController(text: widget.patio.nome);
  late bool _ativo = widget.patio.ativo;
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _nome.dispose();
    super.dispose();
  }

  Future<void> _salvar() async {
    if (_nome.text.trim().isEmpty) {
      setState(() => _erro = 'Informe o nome');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<AdministracaoProvider>().atualizarPatio(widget.patio.id, _nome.text.trim(), _ativo);
      if (mounted) {
        Navigator.of(context).pop();
        mostrarAviso(context, 'Pátio atualizado.');
      }
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Editar pátio'),
        content: SizedBox(
          width: 380,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(controller: _nome, decoration: const InputDecoration(labelText: 'Nome')),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Pátio ativo'),
              subtitle: Text(widget.patio.veiculosEstacionados > 0
                  ? 'Há ${widget.patio.veiculosEstacionados} veículo(s) estacionado(s): não pode ser desativado agora'
                  : 'Desativado não aparece no app nem aceita entradas'),
              value: _ativo,
              onChanged: (v) => setState(() => _ativo = v),
            ),
            if (_erro != null) Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
          ]),
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
