import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../estado/sessao_provider.dart';
import '../servicos/excecao_api.dart';
import '../tema/cores.dart';
import '../util/validadores.dart';
import '../widgets/layout_autenticacao.dart';
import 'acesso/esqueci_senha_dialogo.dart';

class LoginTela extends StatefulWidget {
  const LoginTela({super.key});

  @override
  State<LoginTela> createState() => _LoginTelaState();
}

class _LoginTelaState extends State<LoginTela> {
  final _formulario = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _senha = TextEditingController();
  bool _enviando = false;
  bool _senhaVisivel = false;
  String? _erro;

  @override
  void dispose() {
    _email.dispose();
    _senha.dispose();
    super.dispose();
  }

  Future<void> _entrar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<SessaoProvider>().entrar(_email.text, _senha.text);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => LayoutAutenticacao(
        child: Form(
          key: _formulario,
          child: AutofillGroup(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text('Entrar',
                    style: TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                const SizedBox(height: 20),
                TextFormField(
                  controller: _email,
                  decoration: const InputDecoration(labelText: 'E-mail', prefixIcon: Icon(Icons.mail_outline)),
                  keyboardType: TextInputType.emailAddress,
                  autofillHints: const [AutofillHints.email],
                  textInputAction: TextInputAction.next,
                  validator: Validadores.email,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _senha,
                  decoration: InputDecoration(
                    labelText: 'Senha',
                    prefixIcon: const Icon(Icons.lock_outline),
                    suffixIcon: IconButton(
                      tooltip: _senhaVisivel ? 'Ocultar senha' : 'Mostrar senha',
                      icon: Icon(_senhaVisivel ? Icons.visibility_off : Icons.visibility),
                      onPressed: () => setState(() => _senhaVisivel = !_senhaVisivel),
                    ),
                  ),
                  obscureText: !_senhaVisivel,
                  autofillHints: const [AutofillHints.password],
                  onFieldSubmitted: (_) => _entrar(),
                  validator: (v) => v == null || v.isEmpty ? 'Informe a senha' : null,
                ),
                if (_erro != null) ...[
                  const SizedBox(height: 14),
                  Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                ],
                const SizedBox(height: 22),
                FilledButton(
                  onPressed: _enviando ? null : _entrar,
                  child: _enviando
                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('ENTRAR'),
                ),
                const SizedBox(height: 12),
                TextButton(
                  onPressed: _enviando ? null : () => mostrarEsqueciSenha(context, emailInicial: _email.text),
                  child: const Text('Esqueci minha senha'),
                ),
              ],
            ),
          ),
        ),
      );
}
