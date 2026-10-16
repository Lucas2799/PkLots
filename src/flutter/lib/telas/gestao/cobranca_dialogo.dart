import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../estado/gestao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/caixa_dialogos.dart';
import '../../widgets/recibo_dialogo.dart';

/// Cobrança da saída: escolhe a forma, recebe e libera. Retorna a saída registrada (null = cancelou).
Future<Saida?> mostrarCobranca(BuildContext context, CobrancaSaida cobranca) =>
    showDialog<Saida>(context: context, barrierDismissible: false, builder: (_) => _CobrancaDialogo(cobranca: cobranca));

class _CobrancaDialogo extends StatefulWidget {
  const _CobrancaDialogo({required this.cobranca});

  final CobrancaSaida cobranca;

  @override
  State<_CobrancaDialogo> createState() => _CobrancaDialogoState();
}

class _CobrancaDialogoState extends State<_CobrancaDialogo> {
  late String? _forma = widget.cobranca.formasAceitas.firstOrNull;
  late final _recebido = TextEditingController(text: formatarReais(widget.cobranca.valor));
  bool _enviando = false;
  String? _erro;

  CobrancaSaida get _c => widget.cobranca;

  @override
  void initState() {
    super.initState();
    _recebido.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _recebido.dispose();
    super.dispose();
  }

  double? get _troco {
    final recebido = lerReais(_recebido.text);
    return recebido == null ? null : recebido - _c.valor;
  }

  Future<void> _confirmar() async {
    if (_c.requerPagamento && _forma == 'dinheiro' && (_troco ?? -1) < 0) {
      setState(() => _erro = 'Valor recebido menor que o cobrado.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      final saida = await context.read<GestaoProvider>().confirmarSaida(
            _c.id,
            forma: _c.requerPagamento ? _forma : null,
            valorRecebido: _forma == 'dinheiro' ? lerReais(_recebido.text) : null,
          );
      if (mounted) Navigator.of(context).pop(saida);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final caixaAberto = context.watch<GestaoProvider>().caixa != null;
    final bloqueadoPorCaixa = _c.requerPagamento && !caixaAberto;

    return AlertDialog(
      title: Row(children: [
        const Icon(Icons.point_of_sale, color: CoresPkLots.azulMarinho),
        const SizedBox(width: 8),
        Expanded(child: Text('Saída · ${Formatadores.placa(_c.placa)}')),
      ]),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text(
              [
                if (_c.numeroVaga != null) 'Vaga ${_c.numeroVaga}',
                'Entrada ${Formatadores.dataHora(_c.dataHoraEntrada)}',
                'Permanência ${_c.duracaoFormatada}',
              ].join(' · '),
              style: const TextStyle(color: CoresPkLots.textoSecundario),
            ),
            const SizedBox(height: 12),
            Center(
              child: Text(Formatadores.moeda(_c.valor),
                  style: const TextStyle(fontSize: 36, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho)),
            ),
            if (_c.requerPagamento && _c.detalhamento != null)
              Center(
                child: Text(_c.detalhamento!,
                    textAlign: TextAlign.center, style: const TextStyle(fontSize: 13, color: CoresPkLots.textoSecundario)),
              ),
            if (_c.requerPagamento)
              Center(
                child: Text('Valor válido até ${Formatadores.hora(_c.expiraEm)}',
                    style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
              ),
            if (_c.aviso != null) ...[const SizedBox(height: 10), _Aviso(_c.aviso!, CoresPkLots.manutencao, Icons.warning_amber_rounded)],
            const SizedBox(height: 14),
            if (!_c.requerPagamento)
              _Aviso(
                _c.situacaoPagamento == 'mensalista'
                    ? 'Mensalista${_c.mensalista != null ? ' ${_c.mensalista}' : ''} em dia: sem cobrança.'
                    : 'Dentro da tolerância gratuita: sem cobrança.',
                CoresPkLots.disponivel,
                Icons.check_circle_outline,
              )
            else if (bloqueadoPorCaixa)
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: CoresPkLots.erro.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(10)),
                child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  const Text('Seu caixa está fechado. Abra o caixa para receber.',
                      style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                  const SizedBox(height: 8),
                  OutlinedButton.icon(
                    onPressed: () => mostrarAbrirCaixa(context),
                    icon: const Icon(Icons.lock_open),
                    label: const Text('Abrir caixa'),
                  ),
                ]),
              )
            else ...[
              if (_c.formasAceitas.isEmpty)
                const Text('Nenhuma forma de pagamento habilitada. Peça ao administrador para configurar.',
                    style: TextStyle(color: CoresPkLots.erro)),
              Wrap(spacing: 8, children: [
                for (final forma in _c.formasAceitas)
                  ChoiceChip(
                    avatar: Icon(switch (forma) {
                      'dinheiro' => Icons.payments_outlined,
                      'pix' => Icons.qr_code_2,
                      _ => Icons.credit_card,
                    }, size: 18),
                    label: Text(rotulosFormaPagamento[forma] ?? forma),
                    selected: _forma == forma,
                    onSelected: (_) => setState(() => _forma = forma),
                  ),
              ]),
              const SizedBox(height: 14),
              if (_forma == 'dinheiro') ...[
                TextField(
                  controller: _recebido,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [filtroReais],
                  decoration: const InputDecoration(labelText: 'Valor recebido', prefixText: 'R\$ '),
                  onSubmitted: (_) => _confirmar(),
                ),
                const SizedBox(height: 8),
                if (_troco != null)
                  Text(
                    _troco! >= 0 ? 'Troco: ${Formatadores.moeda(_troco!)}' : 'Faltam ${Formatadores.moeda(-_troco!)}',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      color: _troco! >= 0 ? CoresPkLots.azulMarinho : CoresPkLots.erro,
                    ),
                  ),
              ],
              if (_forma == 'pix' && _c.pixCopiaECola != null) ...[
                Center(
                  child: Container(
                    color: Colors.white,
                    padding: const EdgeInsets.all(8),
                    child: QrImageView(data: _c.pixCopiaECola!, size: 220, semanticsLabel: 'QR Code PIX'),
                  ),
                ),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: () async {
                    await Clipboard.setData(ClipboardData(text: _c.pixCopiaECola!));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('PIX copia e cola copiado.')));
                    }
                  },
                  icon: const Icon(Icons.copy),
                  label: const Text('Copiar PIX copia e cola'),
                ),
                const SizedBox(height: 6),
                const Text('Confira o recebimento no app do banco do pátio antes de liberar.',
                    textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
              ],
              if (_forma == 'cartao')
                const Text('Passe o valor na maquininha do pátio e confirme após a aprovação.',
                    style: TextStyle(color: CoresPkLots.textoSecundario)),
            ],
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
          width: 200,
          child: FilledButton.icon(
            style: FilledButton.styleFrom(backgroundColor: CoresPkLots.ambar, foregroundColor: CoresPkLots.azulMarinho),
            onPressed: _enviando || bloqueadoPorCaixa || (_c.requerPagamento && _forma == null) ? null : _confirmar,
            icon: _enviando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.exit_to_app),
            label: Text(!_c.requerPagamento
                ? 'Liberar saída'
                : switch (_forma) {
                    'pix' => 'PIX recebido · liberar',
                    'cartao' => 'Pago · liberar',
                    _ => 'Receber e liberar',
                  }),
          ),
        ),
      ],
    );
  }
}

class _Aviso extends StatelessWidget {
  const _Aviso(this.texto, this.cor, this.icone);

  final String texto;
  final Color cor;
  final IconData icone;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(color: cor.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(8)),
        child: Row(children: [
          Icon(icone, color: cor),
          const SizedBox(width: 8),
          Expanded(child: Text(texto)),
        ]),
      );
}

/// Fluxo completo da saída usado por todas as telas: cobra → recebe → mostra o recibo.
/// Retorna a saída registrada ou null (cancelada ou erro já avisado).
Future<Saida?> processarSaida(BuildContext context, Future<CobrancaSaida> Function() cobrar) async {
  final CobrancaSaida cobranca;
  try {
    cobranca = await cobrar();
  } on ExcecaoApi catch (e) {
    if (context.mounted) mostrarAvisoCobranca(context, e.mensagem);
    return null;
  }
  if (!context.mounted) return null;
  final saida = await mostrarCobranca(context, cobranca);
  if (saida != null && context.mounted) await mostrarRecibo(context, saida);
  return saida;
}

void mostrarAvisoCobranca(BuildContext context, String mensagem) => ScaffoldMessenger.of(context)
  ..hideCurrentSnackBar()
  ..showSnackBar(SnackBar(content: Text(mensagem), backgroundColor: CoresPkLots.erro));
