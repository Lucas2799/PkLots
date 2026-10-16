import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../widgets/estados_tela.dart';
import 'tabela_preco_tela.dart';

/// Nome do pátio, tabela de preço (resumo, com a tolerância) e mensalistas
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
  late final _mensalidade = TextEditingController(text: _formatarValor(widget.configuracao.valorMensalidade));
  late final _reservadas = TextEditingController(text: '${widget.configuracao.vagasReservadasMensalistas}');
  bool _salvando = false;
  String? _descricaoPreco;

  static String _formatarValor(double valor) => valor.toStringAsFixed(2).replaceAll('.', ',');



  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregarTabela());
  }

  Future<void> _carregarTabela() async {
    try {
      final tabela = await context.read<ClienteApi>().tabelaPreco();
      if (mounted) setState(() => _descricaoPreco = tabela.descricao);
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _descricaoPreco = e.mensagem);
    }
  }

  Future<void> _editarTabela() async {
    final salvou = await Navigator.of(context)
        .push<bool>(MaterialPageRoute(builder: (_) => TabelaPrecoTela(nomePatio: widget.configuracao.nome)));
    if (salvou != true || !mounted) return;
    await _carregarTabela();
    if (mounted) await context.read<AdministracaoProvider>().carregar(); // o valor/hora e a tolerância podem ter mudado
  }

  @override
  void dispose() {
    for (final c in [_nome, _mensalidade, _reservadas]) {
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
          .salvarConfiguracao(_nome.text.trim(), widget.configuracao.valorHora, widget.configuracao.minutosTolerancia,
              valorMensalidade: lerValor(_mensalidade.text) ?? 0, vagasReservadasMensalistas: int.parse(_reservadas.text));
      if (mounted) mostrarAviso(context, 'Configuração salva. Vale para as próximas saídas.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
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
              const SizedBox(height: 18),
              const Text('Tabela de preço', style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
              const SizedBox(height: 6),
              Text(_descricaoPreco ?? 'Carregando...', style: const TextStyle(color: CoresPkLots.textoSecundario)),
              const SizedBox(height: 4),
              Text(
                  'Tolerância gratuita: ${widget.configuracao.minutosTolerancia == 0 ? 'nenhuma' : '${widget.configuracao.minutosTolerancia} min'}',
                  style: const TextStyle(color: CoresPkLots.textoSecundario)),
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  onPressed: _editarTabela,
                  icon: const Icon(Icons.tune),
                  label: const Text('Editar tabela de preço'),
                ),
              ),
              const SizedBox(height: 6),
              const Text('Tolerância, por hora ou 1º período + frações, diária máxima, pernoite e preço de fim de semana, com simulação.',
                  style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Mensalistas', icone: Icons.card_membership_outlined),
              const Text('Mensalista em dia não paga as saídas e pode usar as vagas reservadas.',
                  style: TextStyle(color: CoresPkLots.textoSecundario)),
              const SizedBox(height: 14),
              Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Expanded(
                  child: TextFormField(
                    controller: _mensalidade,
                    decoration: const InputDecoration(labelText: 'Mensalidade', prefixText: 'R\$ ', hintText: 'Ex.: 300,00'),
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[\d.,]'))],
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return null;
                      final valor = lerValor(v);
                      return valor == null || valor < 0 ? 'Valor inválido' : null;
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _reservadas,
                    decoration: InputDecoration(
                      labelText: 'Vagas reservadas',
                      helperText: 'de ${widget.configuracao.totalVagas} vagas',
                    ),
                    keyboardType: TextInputType.number,
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                    validator: (v) {
                      final n = int.tryParse(v ?? '');
                      if (n == null) return 'Informe (0 = sem reserva)';
                      return n > widget.configuracao.totalVagas ? 'Máximo ${widget.configuracao.totalVagas}' : null;
                    },
                  ),
                ),
              ]),
              const SizedBox(height: 6),
              const Text('Vagas reservadas não aparecem como livres para avulsos enquanto não forem usadas por mensalistas.',
                  style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
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
