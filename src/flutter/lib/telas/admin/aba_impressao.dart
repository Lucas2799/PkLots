import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:printing/printing.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../estado/gestao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/documentos_pdf.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../widgets/estados_tela.dart';

/// Impressão do ticket de entrada: modo, impressora de rede, papel e impressão automática
class AbaImpressao extends StatefulWidget {
  const AbaImpressao({super.key, required this.configuracao});

  final ConfiguracaoImpressao configuracao;

  @override
  State<AbaImpressao> createState() => _AbaImpressaoState();
}

class _AbaImpressaoState extends State<AbaImpressao> {
  final _formulario = GlobalKey<FormState>();
  late String _modo = widget.configuracao.modo;
  late final _host = TextEditingController(text: widget.configuracao.impressoraHost ?? '');
  late final _porta = TextEditingController(text: '${widget.configuracao.impressoraPorta}');
  late int _largura = widget.configuracao.larguraPapelMm;
  late bool _automatica = widget.configuracao.imprimirEntradaAutomaticamente;
  bool _salvando = false;
  bool _testando = false;
  late final _redeLiberada = widget.configuracao.modo == ConfiguracaoImpressao.rede ||
      context.read<GestaoProvider>().temRecurso(Recursos.impressoraRede);

  static const _modos = [
    (ConfiguracaoImpressao.desativada, 'Desativada', 'Nenhum ticket é impresso', Icons.print_disabled_outlined),
    (ConfiguracaoImpressao.rede, 'Impressora de rede',
        'Térmica ESC/POS na rede local: o ticket sai direto, sem janela de impressão', Icons.lan_outlined),
    (ConfiguracaoImpressao.navegador, 'Pelo navegador',
        'Impressora instalada no computador (ex.: USB): abre a janela de impressão', Icons.web_outlined),
  ];

  @override
  void dispose() {
    _host.dispose();
    _porta.dispose();
    super.dispose();
  }

  ConfiguracaoImpressao get _atual => ConfiguracaoImpressao(
        modo: _modo,
        impressoraHost: _host.text.trim().isEmpty ? null : _host.text.trim(),
        impressoraPorta: int.tryParse(_porta.text) ?? 9100,
        larguraPapelMm: _largura,
        imprimirEntradaAutomaticamente: _automatica,
      );

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() => _salvando = true);
    try {
      await context.read<AdministracaoProvider>().salvarImpressao(_atual);
      if (mounted) mostrarAviso(context, 'Configuração de impressão salva.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  /// Rede: a API envia uma página de teste (usa a configuração salva). Navegador: ticket de exemplo.
  Future<void> _testar() async {
    setState(() => _testando = true);
    try {
      if (_modo == ConfiguracaoImpressao.rede) {
        await context.read<AdministracaoProvider>().imprimirTesteNaRede();
        if (mounted) mostrarAviso(context, 'Página de teste enviada para a impressora.');
      } else {
        final exemplo = TicketEntrada(
          entradaId: 'teste',
          codigo: 'TESTE123',
          nomePatio: context.read<AdministracaoProvider>().configuracao?.nome ?? 'PkLots',
          placa: 'ABC1D23',
          numeroVaga: 'A1',
          dataHoraEntrada: DateTime.now(),
          valorHora: context.read<AdministracaoProvider>().configuracao?.valorHora ?? 15,
          minutosTolerancia: context.read<AdministracaoProvider>().configuracao?.minutosTolerancia ?? 0,
          larguraPapelMm: _largura,
          conteudoQrCode: 'PKLOTS:TESTE',
        );
        final bytes = Uint8List.fromList(await DocumentosPdf.ticketEntrada(exemplo));
        await Printing.layoutPdf(name: 'ticket-teste', format: DocumentosPdf.formato(_largura), onLayout: (_) async => bytes);
      }
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _testando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final salvo = widget.configuracao;
    final alterado = _atual.paraJson().toString() != salvo.paraJson().toString();

    return Form(
      key: _formulario,
      child: ListView(padding: const EdgeInsets.all(16), children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Ticket de entrada', icone: Icons.confirmation_number_outlined),
              const Text('Impresso para o motorista ao registrar a entrada: placa, vaga, horário, preço e QR Code.',
                  style: TextStyle(color: CoresPkLots.textoSecundario)),
              const SizedBox(height: 8),
              RadioGroup<String>(
                groupValue: _modo,
                onChanged: (valor) => setState(() => _modo = valor ?? _modo),
                child: Column(children: [
                  for (final (valor, rotulo, descricao, icone) in _modos)
                    if (valor == ConfiguracaoImpressao.rede && !_redeLiberada)
                      RadioListTile<String>(
                        value: valor,
                        enabled: false,
                        contentPadding: EdgeInsets.zero,
                        secondary: Icon(icone),
                        title: Text(rotulo),
                        subtitle: const Text('Não incluída no plano da empresa'),
                      )
                    else
                      RadioListTile<String>(
                        value: valor,
                        contentPadding: EdgeInsets.zero,
                        secondary: Icon(icone),
                        title: Text(rotulo),
                        subtitle: Text(descricao),
                      ),
                ]),
              ),
            ]),
          ),
        ),
        if (_modo == ConfiguracaoImpressao.rede) ...[
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const TituloSecao('Impressora de rede', icone: Icons.lan_outlined),
                Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Expanded(
                    flex: 3,
                    child: TextFormField(
                      controller: _host,
                      decoration: const InputDecoration(labelText: 'IP da impressora', hintText: '192.168.0.50'),
                      validator: (v) => v == null || v.trim().isEmpty ? 'Informe o IP' : null,
                      onChanged: (_) => setState(() {}),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: _porta,
                      decoration: const InputDecoration(labelText: 'Porta'),
                      keyboardType: TextInputType.number,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      validator: (v) {
                        final porta = int.tryParse(v ?? '');
                        return porta == null || porta < 1 || porta > 65535 ? 'Inválida' : null;
                      },
                      onChanged: (_) => setState(() {}),
                    ),
                  ),
                ]),
                const SizedBox(height: 6),
                const Text('Impressoras térmicas de rede costumam usar a porta 9100. O IP aparece na página de '
                    'autoteste da impressora (segure o botão de avanço do papel ao ligar).',
                    style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
              ]),
            ),
          ),
        ],
        if (_modo != ConfiguracaoImpressao.desativada) ...[
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const TituloSecao('Papel e automação', icone: Icons.receipt_long_outlined),
                SegmentedButton<int>(
                  segments: const [
                    ButtonSegment(value: 58, label: Text('58 mm')),
                    ButtonSegment(value: 80, label: Text('80 mm')),
                  ],
                  selected: {_largura},
                  onSelectionChanged: (s) => setState(() => _largura = s.first),
                ),
                const SizedBox(height: 8),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Imprimir automaticamente na entrada'),
                  subtitle: const Text('Desligado: imprime só pelo botão de impressora em "Estacionados agora"'),
                  value: _automatica,
                  onChanged: (v) => setState(() => _automatica = v),
                ),
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
        if (_modo != ConfiguracaoImpressao.desativada) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            // A página de teste da rede usa a configuração salva
            onPressed: _testando || (_modo == ConfiguracaoImpressao.rede && alterado) ? null : _testar,
            icon: _testando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.print_outlined),
            label: const Text('Imprimir teste'),
          ),
          if (_modo == ConfiguracaoImpressao.rede && alterado)
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Text('Salve para testar a impressora de rede.',
                  textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
            ),
        ],
      ]),
    );
  }
}
