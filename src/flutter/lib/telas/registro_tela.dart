import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../estado/sessao_provider.dart';
import '../servicos/excecao_api.dart';
import '../tema/cores.dart';
import '../util/validadores.dart';
import '../widgets/layout_autenticacao.dart';

/// Cadastro de motorista
class RegistroTela extends StatefulWidget {
  const RegistroTela({super.key});

  @override
  State<RegistroTela> createState() => _RegistroTelaState();
}

class _RegistroTelaState extends State<RegistroTela> {
  final _formulario = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _email = TextEditingController();
  final _senha = TextEditingController();
  final _confirmacao = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    for (final c in [_nome, _email, _senha, _confirmacao]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _registrar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<SessaoProvider>().registrar(_nome.text, _email.text, _senha.text);
      // A raiz do app troca para a tela inicial; fecha o cadastro
      if (mounted) Navigator.of(context).popUntil((rota) => rota.isFirst);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => LayoutAutenticacao(
        tamanhoLogo: 140,
        child: Form(
          key: _formulario,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(children: [
                IconButton(
                  tooltip: 'Voltar',
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.arrow_back),
                ),
                const Text('Criar conta',
                    style: TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
              ]),
              const SizedBox(height: 16),
              TextFormField(
                controller: _nome,
                decoration: const InputDecoration(labelText: 'Nome completo', prefixIcon: Icon(Icons.person_outline)),
                textCapitalization: TextCapitalization.words,
                textInputAction: TextInputAction.next,
                validator: (v) => Validadores.obrigatorio(v, 'seu nome'),
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _email,
                decoration: const InputDecoration(labelText: 'E-mail', prefixIcon: Icon(Icons.mail_outline)),
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.next,
                validator: Validadores.email,
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _senha,
                decoration: const InputDecoration(
                    labelText: 'Senha', helperText: 'Mínimo de 8 caracteres', prefixIcon: Icon(Icons.lock_outline)),
                obscureText: true,
                textInputAction: TextInputAction.next,
                validator: Validadores.senha,
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _confirmacao,
                decoration: const InputDecoration(labelText: 'Confirmar senha', prefixIcon: Icon(Icons.lock_outline)),
                obscureText: true,
                onFieldSubmitted: (_) => _registrar(),
                validator: (v) => v != _senha.text ? 'As senhas não conferem' : null,
              ),
              if (_erro != null) ...[
                const SizedBox(height: 14),
                Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
              ],
              const SizedBox(height: 22),
              FilledButton(
                onPressed: _enviando ? null : _registrar,
                child: _enviando
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('CRIAR CONTA'),
              ),
            ],
          ),
        ),
      );
}
