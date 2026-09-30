import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';

/// Nome do pátio, valor por hora e tolerância gratuita
class AbaPreco extends StatefulWidget {
  const AbaPreco({super.key, required this.configuracao});

  final ConfiguracaoPatio configuracao;

  /// Aceita "15,50", "1.234,50" e "15.50"
  @visibleForTesting
  static double? lerValor(String texto) {
    final limpo = texto.trim();
    final normalizado = limpo.contains(',') ? limpo.replaceAll('.', '').replaceAll(',', '.') : limpo;
    return double.tryParse(normalizado);
  }

  @override
  State<AbaPreco> createState() => _AbaPrecoState();
}

class _AbaPrecoState extends State<AbaPreco> {
  static double? lerValor(String texto) => AbaPreco.lerValor(texto);

  final _formulario = GlobalKey<FormState>();
  late final _nome = TextEditingController(text: widget.configuracao.nome);
  late final _valorHora = TextEditingController(text: _formatarValor(widget.configuracao.valorHora));
  late final _tolerancia = TextEditingController(text: '${widget.configuracao.minutosTolerancia}');
  bool _salvando = false;

  static const _atalhosTolerancia = [0, 10, 15, 20, 30];

  static String _formatarValor(double valor) => valor.toStringAsFixed(2).replaceAll('.', ',');



  @override
  void initState() {
    super.initState();
    // Atualiza a simulação enquanto digita
    _valorHora.addListener(() => setState(() {}));
    _tolerancia.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    for (final c in [_nome, _valorHora, _tolerancia]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() => _salvando = true);
    try {
      await context
          .read<AdministracaoProvider>()
          .salvarConfiguracao(_nome.text.trim(), lerValor(_valorHora.text)!, int.parse(_tolerancia.text));
      if (mounted) mostrarAviso(context, 'Configuração salva. Vale para as próximas saídas.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  /// Mesma regra da API: arredonda para cima ao minuto; dentro da tolerância é grátis
  static double _simular(int minutos, double valorHora, int tolerancia) =>
      tolerancia > 0 && minutos <= tolerancia ? 0 : ((minutos / 60) * valorHora * 100).roundToDouble() / 100;

  @override
  Widget build(BuildContext context) {
    final valorHora = lerValor(_valorHora.text) ?? 0;
    final tolerancia = int.tryParse(_tolerancia.text) ?? 0;

    return Form(
      key: _formulario,
      child: ListView(padding: const EdgeInsets.all(16), children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Pátio e preço', icone: Icons.payments_outlined),
              TextFormField(
                controller: _nome,
                decoration: const InputDecoration(labelText: 'Nome do pátio'),
                validator: (v) => v == null || v.trim().isEmpty ? 'Informe o nome do pátio' : null,
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _valorHora,
                decoration: const InputDecoration(labelText: 'Valor por hora', prefixText: 'R\$ ', hintText: 'Ex.: 12,50'),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[\d.,]'))],
                validator: (v) {
                  final valor = lerValor(v ?? '');
                  if (valor == null) return 'Informe um valor, ex.: 15,00';
                  if (valor <= 0 || valor > 1000) return 'O valor deve ser maior que zero e até R\$ 1.000,00';
                  return null;
                },
              ),
              const SizedBox(height: 6),
              const Text('A cobrança é proporcional ao tempo, arredondado para cima ao minuto.',
                  style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Tolerância gratuita', icone: Icons.timer_outlined),
              const Text(
                'Quem sair dentro da tolerância não paga. Passou dela, é cobrado o tempo todo.',
                style: TextStyle(color: CoresPkLots.textoSecundario),
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _tolerancia,
                decoration: const InputDecoration(labelText: 'Tolerância', suffixText: 'minutos'),
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                validator: (v) {
                  final minutos = int.tryParse(v ?? '');
                  if (minutos == null) return 'Informe os minutos (0 desativa)';
                  if (minutos > 240) return 'Máximo de 240 minutos';
                  return null;
                },
              ),
              const SizedBox(height: 10),
              Wrap(spacing: 8, children: [
                for (final minutos in _atalhosTolerancia)
                  ChoiceChip(
                    label: Text(minutos == 0 ? 'Sem tolerância' : '$minutos min'),
                    selected: tolerancia == minutos,
                    onSelected: (_) => _tolerancia.text = '$minutos',
                  ),
              ]),
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Simulação', icone: Icons.calculate_outlined),
              for (final (rotulo, minutos) in [('10 min', 10), ('16 min', 16), ('30 min', 30), ('1h', 60), ('2h 30min', 150)])
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Row(children: [
                    Expanded(child: Text('Ficou $rotulo', style: const TextStyle(color: CoresPkLots.textoSecundario))),
                    Text(
                      _simular(minutos, valorHora, tolerancia) == 0 && valorHora > 0
                          ? 'Grátis'
                          : Formatadores.moeda(_simular(minutos, valorHora, tolerancia)),
                      style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                    ),
                  ]),
                ),
            ]),
          ),
        ),
        const SizedBox(height: 20),
        FilledButton.icon(
          onPressed: _salvando ? null : _salvar,
          icon: _salvando
              ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.save_outlined),
          label: const Text('Salvar'),
        ),
        const SizedBox(height: 8),
        const Text('Saídas já registradas não mudam de valor.',
            textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
      ]),
    );
  }
}
