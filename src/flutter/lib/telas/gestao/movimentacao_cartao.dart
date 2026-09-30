import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../servicos/excecao_api.dart';
import '../../util/formatadores.dart';
import '../../util/validadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/recibo_dialogo.dart';

/// Registro de entrada/saída: digitando a placa ou enviando a foto (LPR)
class MovimentacaoCartao extends StatefulWidget {
  const MovimentacaoCartao({super.key});

  @override
  State<MovimentacaoCartao> createState() => _MovimentacaoCartaoState();
}

class _MovimentacaoCartaoState extends State<MovimentacaoCartao> {
  final _formulario = GlobalKey<FormState>();
  final _placa = TextEditingController();
  bool _enviando = false;

  @override
  void dispose() {
    _placa.dispose();
    super.dispose();
  }

  GestaoProvider get _estado => context.read<GestaoProvider>();

  Future<void> _executar(Future<void> Function() acao) async {
    setState(() => _enviando = true);
    try {
      await acao();
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  Future<void> _entradaManual() async {
    if (!_formulario.currentState!.validate()) return;
    await _executar(() async {
      final entrada = await _estado.registrarEntrada(Validadores.normalizarPlaca(_placa.text));
      _placa.clear();
      if (mounted) {
        mostrarAviso(context, 'Entrada de ${Formatadores.placa(entrada.placa)} registrada na vaga ${entrada.numeroVaga}');
      }
    });
  }

  Future<void> _saidaManual() async {
    if (!_formulario.currentState!.validate()) return;
    await _executar(() async {
      final saida = await _estado.registrarSaida(Validadores.normalizarPlaca(_placa.text));
      _placa.clear();
      if (mounted) await mostrarRecibo(context, saida);
    });
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
    await _executar(() async {
      if (entrada) {
        final registro = await _estado.registrarEntradaPorImagem(bytes, foto.name);
        if (mounted) {
          mostrarAviso(context,
              'Placa ${Formatadores.placa(registro.placa)} reconhecida · vaga ${registro.numeroVaga}');
        }
      } else {
        final saida = await _estado.registrarSaidaPorImagem(bytes, foto.name);
        if (mounted) await mostrarRecibo(context, saida);
      }
    });
  }

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formulario,
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const TituloSecao('Entrada e saída', icone: Icons.swap_horiz),
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
              if (_enviando) ...[const SizedBox(height: 12), const LinearProgressIndicator()],
            ]),
          ),
        ),
      );
}
