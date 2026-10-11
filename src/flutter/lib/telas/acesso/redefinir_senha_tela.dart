import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/validadores.dart';
import '../../widgets/layout_autenticacao.dart';

/// Nova senha pelo link do e-mail (?redefinir=token). Ao concluir, volta para o login.
class RedefinirSenhaTela extends StatefulWidget {
  const RedefinirSenhaTela({super.key, required this.token, required this.aoConcluir});

  final String token;
  final VoidCallback aoConcluir;

  @override
  State<RedefinirSenhaTela> createState() => _RedefinirSenhaTelaState();
}

class _RedefinirSenhaTelaState extends State<RedefinirSenhaTela> {
  final _formulario = GlobalKey<FormState>();
  final _nova = TextEditingController();
  final _confirmacao = TextEditingController();
  bool _enviando = false;
  bool _concluido = false;
  String? _erro;

  @override
  void dispose() {
    _nova.dispose();
    _confirmacao.dispose();
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<ClienteApi>().redefinirSenhaPorEmail(widget.token, _nova.text);
      setState(() => _concluido = true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => LayoutAutenticacao(
    tamanhoLogo: 140,
    child: _concluido
        ? Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Icon(Icons.check_circle_outline, size: 48, color: CoresPkLots.disponivel),
              const SizedBox(height: 12),
              const Text(
                'Senha alterada',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
              ),
              const SizedBox(height: 8),
              const Text('Entre com a nova senha.', textAlign: TextAlign.center),
              const SizedBox(height: 20),
              FilledButton(onPressed: widget.aoConcluir, child: const Text('IR PARA O LOGIN')),
            ],
          )
        : Form(
            key: _formulario,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Criar nova senha',
                  style: TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Mínimo de 8 caracteres, com letras e números.',
                  style: TextStyle(color: CoresPkLots.textoSecundario),
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _nova,
                  obscureText: true,
                  autofillHints: const [AutofillHints.newPassword],
                  decoration: const InputDecoration(labelText: 'Nova senha', prefixIcon: Icon(Icons.lock_outline)),
                  validator: Validadores.novaSenha,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _confirmacao,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Repita a nova senha',
                    prefixIcon: Icon(Icons.lock_outline),
                  ),
                  validator: (v) => v != _nova.text ? 'As senhas não conferem' : null,
                  onFieldSubmitted: (_) => _salvar(),
                ),
                if (_erro != null) ...[
                  const SizedBox(height: 14),
                  Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                ],
                const SizedBox(height: 22),
                FilledButton(
                  onPressed: _enviando ? null : _salvar,
                  child: _enviando
                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('SALVAR NOVA SENHA'),
                ),
                const SizedBox(height: 8),
                TextButton(onPressed: _enviando ? null : widget.aoConcluir, child: const Text('Voltar ao login')),
              ],
            ),
          ),
  );
}
