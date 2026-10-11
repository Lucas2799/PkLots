import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/validadores.dart';
import '../../widgets/layout_autenticacao.dart';

/// Convite de admin/operador (?ativar=token): mostra empresa, perfil e pátios, cria a senha e entra
class AtivarContaTela extends StatefulWidget {
  const AtivarContaTela({super.key, required this.token, required this.aoConcluir});

  final String token;
  final VoidCallback aoConcluir;

  @override
  State<AtivarContaTela> createState() => _AtivarContaTelaState();
}

class _AtivarContaTelaState extends State<AtivarContaTela> {
  final _formulario = GlobalKey<FormState>();
  final _senha = TextEditingController();
  final _confirmacao = TextEditingController();
  ConviteUsuario? _convite;
  String? _erroCarga;
  String? _erro;
  bool _enviando = false;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  @override
  void dispose() {
    _senha.dispose();
    _confirmacao.dispose();
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final convite = await context.read<ClienteApi>().conviteUsuario(widget.token);
      if (mounted) setState(() => _convite = convite);
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erroCarga = e.mensagem);
    }
  }

  Future<void> _ativar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    final sessao = context.read<SessaoProvider>();
    try {
      final token = await context.read<ClienteApi>().ativarConta(widget.token, _senha.text);
      await sessao.entrarComToken(token);
      widget.aoConcluir();
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final convite = _convite;
    return LayoutAutenticacao(
      tamanhoLogo: 140,
      child: _erroCarga != null
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Icon(Icons.link_off, size: 48, color: CoresPkLots.erro),
                const SizedBox(height: 12),
                const Text(
                  'Convite indisponível',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                ),
                const SizedBox(height: 8),
                Text(_erroCarga!, textAlign: TextAlign.center),
                const SizedBox(height: 20),
                FilledButton(onPressed: widget.aoConcluir, child: const Text('IR PARA O LOGIN')),
              ],
            )
          : convite == null
          ? const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            )
          : Form(
              key: _formulario,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Olá, ${convite.nomeCompleto.split(' ').first}!',
                    style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Você foi cadastrado(a) na ${convite.nomeEmpresa} como '
                    '${convite.perfil == 'admin' ? 'Administrador(a)' : 'Operador(a)'}.',
                    style: const TextStyle(color: CoresPkLots.textoSecundario),
                  ),
                  if (convite.patios.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final patio in convite.patios)
                          Chip(avatar: const Icon(Icons.local_parking, size: 18), label: Text(patio)),
                      ],
                    ),
                  ],
                  const SizedBox(height: 12),
                  Text('Login: ${convite.email}', style: const TextStyle(fontWeight: FontWeight.w600)),
                  const SizedBox(height: 16),
                  const Text(
                    'Crie a sua senha (mínimo de 8 caracteres, com letras e números).',
                    style: TextStyle(color: CoresPkLots.textoSecundario),
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _senha,
                    obscureText: true,
                    autofillHints: const [AutofillHints.newPassword],
                    decoration: const InputDecoration(labelText: 'Senha', prefixIcon: Icon(Icons.lock_outline)),
                    validator: Validadores.novaSenha,
                  ),
                  const SizedBox(height: 14),
                  TextFormField(
                    controller: _confirmacao,
                    obscureText: true,
                    decoration: const InputDecoration(
                      labelText: 'Repita a senha',
                      prefixIcon: Icon(Icons.lock_outline),
                    ),
                    validator: (v) => v != _senha.text ? 'As senhas não conferem' : null,
                    onFieldSubmitted: (_) => _ativar(),
                  ),
                  if (_erro != null) ...[
                    const SizedBox(height: 14),
                    Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                  ],
                  const SizedBox(height: 22),
                  FilledButton(
                    onPressed: _enviando ? null : _ativar,
                    child: _enviando
                        ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('CRIAR SENHA E ENTRAR'),
                  ),
                ],
              ),
            ),
    );
  }
}
