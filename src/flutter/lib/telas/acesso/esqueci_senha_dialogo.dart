import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/validadores.dart';

/// Pede o link de redefinição de senha por e-mail
Future<void> mostrarEsqueciSenha(BuildContext context, {String emailInicial = ''}) => showDialog<void>(
  context: context,
  builder: (_) => _EsqueciSenhaDialogo(emailInicial: emailInicial),
);

class _EsqueciSenhaDialogo extends StatefulWidget {
  const _EsqueciSenhaDialogo({required this.emailInicial});

  final String emailInicial;

  @override
  State<_EsqueciSenhaDialogo> createState() => _EsqueciSenhaDialogoState();
}

class _EsqueciSenhaDialogoState extends State<_EsqueciSenhaDialogo> {
  final _formulario = GlobalKey<FormState>();
  late final _email = TextEditingController(text: widget.emailInicial.trim());
  bool _enviando = false;
  bool _enviado = false;
  String? _erro;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _enviar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<ClienteApi>().esqueciSenha(_email.text.trim());
      setState(() => _enviado = true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Esqueci minha senha'),
    content: SizedBox(
      width: 380,
      child: _enviado
          ? Text(
              'Se houver uma conta com ${_email.text.trim()}, enviamos um link para criar uma nova senha. '
              'Ele vale por 1 hora. Confira também a caixa de spam.',
              style: const TextStyle(color: CoresPkLots.textoSecundario),
            )
          : Form(
              key: _formulario,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text(
                    'Informe o e-mail da sua conta. Enviaremos um link para criar uma nova senha.',
                    style: TextStyle(color: CoresPkLots.textoSecundario),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _email,
                    autofocus: true,
                    keyboardType: TextInputType.emailAddress,
                    decoration: const InputDecoration(labelText: 'E-mail', prefixIcon: Icon(Icons.mail_outline)),
                    validator: Validadores.email,
                    onFieldSubmitted: (_) => _enviar(),
                  ),
                  if (_erro != null) ...[
                    const SizedBox(height: 10),
                    Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                  ],
                ],
              ),
            ),
    ),
    actions: _enviado
        ? [FilledButton(onPressed: () => Navigator.of(context).pop(), child: const Text('OK'))]
        : [
            TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(), child: const Text('Cancelar')),
            SizedBox(
              width: 140,
              child: FilledButton(
                onPressed: _enviando ? null : _enviar,
                child: _enviando
                    ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Enviar link'),
              ),
            ),
          ],
  );
}
