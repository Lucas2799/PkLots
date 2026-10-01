import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../estado/sessao_provider.dart';
import '../servicos/excecao_api.dart';
import '../tema/cores.dart';
import '../widgets/estados_tela.dart';
import '../widgets/layout_autenticacao.dart';

/// Troca de senha. Obrigatória após senha temporária/pública (só permite trocar ou sair);
/// voluntária quando aberta pelo menu (volta à tela anterior).
class TrocaSenhaTela extends StatefulWidget {
  const TrocaSenhaTela({super.key, this.obrigatoria = false});

  final bool obrigatoria;

  @override
  State<TrocaSenhaTela> createState() => _TrocaSenhaTelaState();
}

class _TrocaSenhaTelaState extends State<TrocaSenhaTela> {
  final _formulario = GlobalKey<FormState>();
  final _atual = TextEditingController();
  final _nova = TextEditingController();
  final _confirmacao = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    for (final c in [_atual, _nova, _confirmacao]) {
      c.dispose();
    }
    super.dispose();
  }

  // Mesmas regras da API (mensagens imediatas); a API valida de novo
  static String? _validarNova(String? senha) {
    if (senha == null || senha.length < 8) return 'Mínimo de 8 caracteres';
    if (!senha.contains(RegExp(r'[A-Za-z]')) || !senha.contains(RegExp(r'\d'))) return 'Use letras e números';
    return null;
  }

  Future<void> _trocar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<SessaoProvider>().trocarSenha(_atual.text, _nova.text);
      if (!mounted) return;
      if (!widget.obrigatoria) {
        Navigator.of(context).pop();
        mostrarAviso(context, 'Senha alterada.');
      }
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final formulario = Form(
      key: _formulario,
      child: AutofillGroup(
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text(widget.obrigatoria ? 'Defina sua senha' : 'Alterar senha',
              style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
          const SizedBox(height: 8),
          if (widget.obrigatoria)
            const Text(
              'Sua senha atual é temporária ou conhecida. Para continuar, escolha uma senha nova só sua.',
              style: TextStyle(color: CoresPkLots.textoSecundario),
            ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _atual,
            obscureText: true,
            autofillHints: const [AutofillHints.password],
            decoration: const InputDecoration(labelText: 'Senha atual', prefixIcon: Icon(Icons.lock_outline)),
            validator: (v) => v == null || v.isEmpty ? 'Informe a senha atual' : null,
          ),
          const SizedBox(height: 14),
          TextFormField(
            controller: _nova,
            obscureText: true,
            autofillHints: const [AutofillHints.newPassword],
            decoration: const InputDecoration(
              labelText: 'Nova senha',
              helperText: 'Mínimo de 8 caracteres, com letras e números',
              prefixIcon: Icon(Icons.key_outlined),
            ),
            validator: _validarNova,
          ),
          const SizedBox(height: 14),
          TextFormField(
            controller: _confirmacao,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Confirmar nova senha', prefixIcon: Icon(Icons.key_outlined)),
            validator: (v) => v != _nova.text ? 'As senhas não conferem' : null,
            onFieldSubmitted: (_) => _trocar(),
          ),
          if (_erro != null) ...[
            const SizedBox(height: 14),
            Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
          ],
          const SizedBox(height: 22),
          FilledButton(
            onPressed: _enviando ? null : _trocar,
            child: _enviando
                ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('SALVAR NOVA SENHA'),
          ),
          if (widget.obrigatoria) ...[
            const SizedBox(height: 12),
            OutlinedButton(onPressed: context.read<SessaoProvider>().sair, child: const Text('Sair')),
          ],
        ]),
      ),
    );

    if (widget.obrigatoria) return LayoutAutenticacao(tamanhoLogo: 140, child: formulario);
    return Scaffold(
      appBar: AppBar(title: const Text('Alterar senha')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Card(child: Padding(padding: const EdgeInsets.all(24), child: formulario)),
          ),
        ),
      ),
    );
  }
}
