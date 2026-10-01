import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../widgets/estados_tela.dart';

/// Como a empresa recebe: provedor, chave PIX do dono do pátio e formas aceitas
class AbaPagamentos extends StatefulWidget {
  const AbaPagamentos({super.key});

  @override
  State<AbaPagamentos> createState() => _AbaPagamentosState();
}

class _AbaPagamentosState extends State<AbaPagamentos> {
  final _formulario = GlobalKey<FormState>();
  final _chave = TextEditingController();
  final _nome = TextEditingController();
  final _cidade = TextEditingController();
  String _provedor = 'pix_manual';
  bool _dinheiro = true, _pix = true, _cartao = true;
  bool _carregando = true, _salvando = false;
  String? _pixTeste;

  static const _provedores = {
    'pix_manual': ('PIX com chave própria', 'O QR é gerado com a chave do pátio; o operador confirma o recebimento no app do banco.'),
  };

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  @override
  void dispose() {
    for (final c in [_chave, _nome, _cidade]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final c = await _api.configuracaoPagamento();
      setState(() {
        _provedor = c.provedor;
        _chave.text = c.chavePix ?? '';
        _nome.text = c.nomeRecebedor ?? '';
        _cidade.text = c.cidadeRecebedor ?? '';
        _dinheiro = c.aceitaDinheiro;
        _pix = c.aceitaPix;
        _cartao = c.aceitaCartao;
        _carregando = false;
      });
    } on ExcecaoApi catch (e) {
      if (mounted) {
        setState(() => _carregando = false);
        mostrarAviso(context, e.mensagem, erro: true);
      }
    }
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() => _salvando = true);
    String? texto(TextEditingController c) => c.text.trim().isEmpty ? null : c.text.trim();
    try {
      await _api.salvarConfiguracaoPagamento(ConfiguracaoPagamento(
        provedor: _provedor,
        chavePix: texto(_chave),
        nomeRecebedor: texto(_nome),
        cidadeRecebedor: texto(_cidade),
        aceitaDinheiro: _dinheiro,
        aceitaPix: _pix,
        aceitaCartao: _cartao,
      ));
      if (mounted) mostrarAviso(context, 'Configuração de pagamento salva.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  Future<void> _testarPix() async {
    try {
      final pix = await _api.pixDeTeste();
      setState(() => _pixTeste = pix);
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_carregando) return const Center(child: CircularProgressIndicator());
    final exigePix = _pix;

    return Form(
      key: _formulario,
      child: ListView(padding: const EdgeInsets.all(16), children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Provedor de pagamento', icone: Icons.account_balance_outlined),
              const Text(
                'O dinheiro vai direto para a conta do pátio. O contrato com banco ou provedor é da sua empresa; '
                'o PkLots só gera as cobranças e registra os pagamentos.',
                style: TextStyle(color: CoresPkLots.textoSecundario),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _provedor,
                decoration: const InputDecoration(labelText: 'Provedor'),
                items: [
                  for (final MapEntry(key: id, value: (nome, _)) in _provedores.entries)
                    DropdownMenuItem(value: id, child: Text(nome)),
                ],
                onChanged: (v) => setState(() => _provedor = v ?? _provedor),
              ),
              const SizedBox(height: 6),
              Text(_provedores[_provedor]?.$2 ?? '', style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
              const SizedBox(height: 4),
              const Text('Provedores com confirmação automática (Mercado Pago, Asaas...) entram como novas opções.',
                  style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Formas aceitas na saída', icone: Icons.payments_outlined),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Dinheiro'),
                subtitle: const Text('Com cálculo de troco e conferência no fechamento do caixa'),
                value: _dinheiro,
                onChanged: (v) => setState(() => _dinheiro = v),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('PIX'),
                subtitle: const Text('QR Code com o valor exato, na chave do pátio'),
                value: _pix,
                onChanged: (v) => setState(() => _pix = v),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Cartão'),
                subtitle: const Text('Na maquininha do pátio; o operador confirma após a aprovação'),
                value: _cartao,
                onChanged: (v) => setState(() => _cartao = v),
              ),
            ]),
          ),
        ),
        if (exigePix) ...[
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const TituloSecao('Dados do PIX', icone: Icons.qr_code_2),
                TextFormField(
                  controller: _chave,
                  decoration: const InputDecoration(
                      labelText: 'Chave PIX do pátio', helperText: 'CPF, CNPJ, e-mail, telefone (+55...) ou chave aleatória'),
                  validator: (v) => v == null || v.trim().isEmpty ? 'Informe a chave PIX' : null,
                ),
                const SizedBox(height: 12),
                Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Expanded(
                    flex: 3,
                    child: TextFormField(
                      controller: _nome,
                      maxLength: 25,
                      decoration: const InputDecoration(labelText: 'Nome do recebedor', helperText: 'Como aparece no banco'),
                      validator: (v) => v == null || v.trim().isEmpty ? 'Informe o nome' : null,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 2,
                    child: TextFormField(
                      controller: _cidade,
                      maxLength: 15,
                      decoration: const InputDecoration(labelText: 'Cidade'),
                      validator: (v) => v == null || v.trim().isEmpty ? 'Informe a cidade' : null,
                    ),
                  ),
                ]),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: _testarPix,
                  icon: const Icon(Icons.qr_code),
                  label: const Text('Gerar PIX de teste (R\$ 1,00)'),
                ),
                if (_pixTeste != null) ...[
                  const SizedBox(height: 12),
                  Center(
                    child: Container(
                      color: Colors.white,
                      padding: const EdgeInsets.all(8),
                      child: QrImageView(data: _pixTeste!, size: 200, semanticsLabel: 'PIX de teste'),
                    ),
                  ),
                  const SizedBox(height: 6),
                  const Text('Leia com o app do banco: deve aparecer o nome do recebedor e R\$ 1,00. Salve antes de testar.',
                      textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
                ],
              ]),
            ),
          ),
        ],
        const SizedBox(height: 20),
        FilledButton.icon(
          onPressed: _salvando ? null : _salvar,
          icon: _salvando
              ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.save_outlined),
          label: const Text('Salvar'),
        ),
      ]),
    );
  }
}
