import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/impressao_tickets.dart';
import '../../servicos/excecao_api.dart';
import '../../util/formatadores.dart';
import '../../util/validadores.dart';
import '../../widgets/estados_tela.dart';
import '../../tema/cores.dart';
import '../../widgets/caixa_dialogos.dart';
import 'cobranca_dialogo.dart';
import 'leitor_ticket_tela.dart';

/// Registro de entrada/saída: digitando a placa ou enviando a foto (LPR)
class MovimentacaoCartao extends StatefulWidget {
  const MovimentacaoCartao({super.key});

  @override
  State<MovimentacaoCartao> createState() => _MovimentacaoCartaoState();
}

class _MovimentacaoCartaoState extends State<MovimentacaoCartao> {
  final _formulario = GlobalKey<FormState>();
  final _placa = TextEditingController();
  final _ticket = TextEditingController();
  final _focoTicket = FocusNode();
  bool _enviando = false;

  @override
  void dispose() {
    _placa.dispose();
    _ticket.dispose();
    _focoTicket.dispose();
    super.dispose();
  }

  /// Leitor USB "digita" o conteúdo do QR e envia Enter; a câmera devolve o mesmo texto
  Future<void> _saidaPorTicket(String lido) async {
    if (lido.trim().isEmpty) return;
    final cobranca = await _executar(() => _estado.cobrarSaidaPorTicket(lido.trim()));
    _ticket.clear();
    if (cobranca != null && mounted) await processarSaida(context, () async => cobranca);
    // Pronto para o próximo ticket
    if (mounted) _focoTicket.requestFocus();
  }

  Future<void> _lerTicketPelaCamera() async {
    final lido = await Navigator.of(context).push<String>(MaterialPageRoute(builder: (_) => const LeitorTicketTela()));
    if (lido != null && mounted) await _saidaPorTicket(lido);
  }

  GestaoProvider get _estado => context.read<GestaoProvider>();

  /// Executa a chamada com indicador de progresso; em erro mostra o aviso e retorna null.
  /// Diálogos (recibo, impressão) são abertos depois, com o progresso já encerrado.
  Future<T?> _executar<T>(Future<T> Function() acao) async {
    setState(() => _enviando = true);
    try {
      return await acao();
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
      return null;
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  /// Após a entrada: avisa o resultado da impressão automática ou imprime pelo navegador
  Future<void> _aposEntrada(Entrada entrada, String mensagem) async {
    final impressao = ImpressaoTickets(context.read<ClienteApi>());
    if (entrada.mensalista != null) {
      if (entrada.situacaoMensalista == Mensalista.vencido) {
        // Destaca a mensalidade vencida (o operador pode registrar o pagamento antes da saída)
        mostrarAviso(context, '$mensagem · Mensalidade de ${entrada.mensalista} vencida: será cobrado como avulso.',
            erro: true);
        return;
      }
      mensagem = '$mensagem · Mensalista ${entrada.mensalista}'
          '${entrada.mensalidadeValidaAte != null ? ' (em dia até ${Formatadores.data(entrada.mensalidadeValidaAte!)})' : ''}';
    }
    switch (entrada.impressao) {
      case 'impresso':
        mostrarAviso(context, '$mensagem · ticket impresso');
      case 'falhou':
        mostrarAviso(
          context,
          '$mensagem, mas a impressora não respondeu.',
          erro: true,
          acao: SnackBarAction(
            label: 'Tentar de novo',
            textColor: Colors.white,
            onPressed: () => impressao.reimprimir(entrada.id).catchError((_) => false),
          ),
        );
      case 'navegador':
        mostrarAviso(context, mensagem);
        await impressao.imprimirPeloNavegador(entrada.id);
      default:
        mostrarAviso(context, mensagem);
    }
  }

  Future<void> _entradaManual() async {
    if (!_formulario.currentState!.validate()) return;
    final entrada = await _executar(() => _estado.registrarEntrada(Validadores.normalizarPlaca(_placa.text)));
    if (entrada == null || !mounted) return;
    _placa.clear();
    await _aposEntrada(entrada, 'Entrada de ${Formatadores.placa(entrada.placa)} registrada na vaga ${entrada.numeroVaga}');
  }

  Future<void> _saidaManual() async {
    if (!_formulario.currentState!.validate()) return;
    final cobranca = await _executar(() => _estado.cobrarSaida(Validadores.normalizarPlaca(_placa.text)));
    if (cobranca == null || !mounted) return;
    final saida = await processarSaida(context, () async => cobranca);
    if (saida != null) _placa.clear();
  }

  Future<XFile?> _escolherFoto() async {
    var origem = ImageSource.gallery;
    if (!kIsWeb) {
      final escolhida = await showModalBottomSheet<ImageSource>(
        context: context,
        builder: (context) => SafeArea(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            ListTile(
              leading: const Icon(Icons.photo_camera),
              title: const Text('Tirar foto'),
              onTap: () => Navigator.of(context).pop(ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Escolher da galeria'),
              onTap: () => Navigator.of(context).pop(ImageSource.gallery),
            ),
          ]),
        ),
      );
      if (escolhida == null) return null;
      origem = escolhida;
    }
    return ImagePicker().pickImage(source: origem, maxWidth: 1920, imageQuality: 90);
  }

  Future<void> _porFoto({required bool entrada}) async {
    final foto = await _escolherFoto();
    if (foto == null) return;
    final bytes = await foto.readAsBytes();
    if (!mounted) return;
    if (entrada) {
      final registro = await _executar(() => _estado.registrarEntradaPorImagem(bytes, foto.name));
      if (registro != null && mounted) {
        await _aposEntrada(registro, 'Placa ${Formatadores.placa(registro.placa)} reconhecida · vaga ${registro.numeroVaga}');
      }
    } else {
      final cobranca = await _executar(() => _estado.cobrarSaidaPorImagem(bytes, foto.name));
      if (cobranca != null && mounted) await processarSaida(context, () async => cobranca);
    }
  }

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formulario,
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Entrada e saída', icone: Icons.swap_horiz),
              _SituacaoCaixa(caixa: context.watch<GestaoProvider>().caixa),
              const SizedBox(height: 12),
              TextFormField(
                controller: _placa,
                enabled: !_enviando,
                decoration: const InputDecoration(labelText: 'Placa', hintText: 'ABC1234 ou ABC1D23'),
                textCapitalization: TextCapitalization.characters,
                validator: Validadores.placa,
                onFieldSubmitted: (_) => _entradaManual(),
              ),
              const SizedBox(height: 12),
              Row(children: [
                Expanded(
                  child: FilledButton.icon(
                    onPressed: _enviando ? null : _entradaManual,
                    icon: const Icon(Icons.login),
                    label: const Text('Entrada'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: _enviando ? null : _saidaManual,
                    icon: const Icon(Icons.logout),
                    label: const Text('Saída'),
                  ),
                ),
              ]),
              // Leitura automática de placa: só se o plano incluir
              if (context.watch<GestaoProvider>().temRecurso(Recursos.lpr)) ...[
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 12),
                  child: Row(children: [
                    Expanded(child: Divider()),
                    Padding(padding: EdgeInsets.symmetric(horizontal: 8), child: Text('ou pela foto da placa')),
                    Expanded(child: Divider()),
                  ]),
                ),
                Row(children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _enviando ? null : () => _porFoto(entrada: true),
                      icon: const Icon(Icons.photo_camera_outlined),
                      label: const Text('Entrada'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _enviando ? null : () => _porFoto(entrada: false),
                      icon: const Icon(Icons.photo_camera_outlined),
                      label: const Text('Saída'),
                    ),
                  ),
                ]),
              ],
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Row(children: [
                  Expanded(child: Divider()),
                  Padding(padding: EdgeInsets.symmetric(horizontal: 8), child: Text('ou saída pelo ticket')),
                  Expanded(child: Divider()),
                ]),
              ),
              TextField(
                controller: _ticket,
                focusNode: _focoTicket,
                enabled: !_enviando,
                textCapitalization: TextCapitalization.characters,
                textInputAction: TextInputAction.done,
                decoration: InputDecoration(
                  labelText: 'Ler ticket',
                  hintText: 'Passe o leitor ou digite o código (ex.: K7M2-Q9XD)',
                  prefixIcon: const Icon(Icons.qr_code_scanner),
                  suffixIcon: kIsWeb
                      ? null
                      : IconButton(
                          tooltip: 'Ler com a câmera',
                          icon: const Icon(Icons.photo_camera_outlined),
                          onPressed: _enviando ? null : _lerTicketPelaCamera,
                        ),
                ),
                onSubmitted: _saidaPorTicket,
              ),
              if (_enviando) ...[const SizedBox(height: 12), const LinearProgressIndicator()],
            ]),
          ),
        ),
      );
}

/// Lembra o operador de abrir o caixa antes de receber
class _SituacaoCaixa extends StatelessWidget {
  const _SituacaoCaixa({required this.caixa});

  final Caixa? caixa;

  @override
  Widget build(BuildContext context) {
    final aberto = caixa != null;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: (aberto ? CoresPkLots.disponivel : CoresPkLots.manutencao).withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(children: [
        Icon(aberto ? Icons.lock_open : Icons.lock_outline,
            size: 18, color: aberto ? CoresPkLots.disponivel : CoresPkLots.manutencao),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            aberto
                ? 'Caixa aberto · ${Formatadores.moeda(caixa!.dinheiroEsperado)} em dinheiro'
                : 'Caixa fechado: abra para receber pagamentos',
            style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho),
          ),
        ),
        if (!aberto)
          TextButton(onPressed: () => mostrarAbrirCaixa(context), child: const Text('Abrir')),
      ]),
    );
  }
}
