import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/validadores.dart';
import '../../widgets/layout_autenticacao.dart';

/// Convite de mensalista aberto pelo link do e-mail (?convite=token).
/// Conta nova: define a senha e já entra. Conta existente: só confirma e entra com a senha de sempre.
class ConviteTela extends StatefulWidget {
  const ConviteTela({super.key, required this.token, required this.aoConcluir});

  final String token;
  final VoidCallback aoConcluir;

  @override
  State<ConviteTela> createState() => _ConviteTelaState();
}

class _ConviteTelaState extends State<ConviteTela> {
  final _formulario = GlobalKey<FormState>();
  final _senha = TextEditingController();
  final _confirmacao = TextEditingController();
  ConviteMensalista? _convite;
  String? _erroCarga;
  String? _erro;
  bool _enviando = false;
  bool _vinculadoContaExistente = false;

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
      final convite = await context.read<ClienteApi>().convite(widget.token);
      if (mounted) setState(() => _convite = convite);
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erroCarga = e.mensagem);
    }
  }

  Future<void> _aceitar() async {
    final convite = _convite!;
    if (!convite.contaExistente && !_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    final sessao = context.read<SessaoProvider>();
    try {
      final token = await context.read<ClienteApi>().aceitarConvite(
        widget.token,
        senha: convite.contaExistente ? null : _senha.text,
      );
      if (token != null) {
        await sessao.entrarComToken(token);
        widget.aoConcluir();
      } else {
        setState(() => _vinculadoContaExistente = true);
      }
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
          ? _Mensagem(
              icone: Icons.link_off,
              cor: CoresPkLots.erro,
              titulo: 'Convite indisponível',
              texto: _erroCarga!,
              botao: 'IR PARA O LOGIN',
              aoPressionar: widget.aoConcluir,
            )
          : convite == null
          ? const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            )
          : _vinculadoContaExistente
          ? _Mensagem(
              icone: Icons.check_circle_outline,
              cor: CoresPkLots.disponivel,
              titulo: 'Acesso liberado',
              texto:
                  'O contrato do ${convite.nomePatio} foi adicionado à sua conta. Entre com o e-mail '
                  '${convite.email} e a sua senha.',
              botao: 'IR PARA O LOGIN',
              aoPressionar: widget.aoConcluir,
            )
          : Form(
              key: _formulario,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Olá, ${convite.nomeMensalista.split(' ').first}!',
                    style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'O ${convite.nomePatio} liberou seu acesso ao app como mensalista.',
                    style: const TextStyle(color: CoresPkLots.textoSecundario),
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final placa in convite.placasMascaradas)
                        Chip(avatar: const Icon(Icons.directions_car, size: 18), label: Text(placa)),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Text('Conta: ${convite.email}', style: const TextStyle(fontWeight: FontWeight.w600)),
                  const SizedBox(height: 16),
                  if (convite.contaExistente)
                    const Text(
                      'Você já tem conta no PkLots com este e-mail. Confirme para adicionar este contrato a ela.',
                      style: TextStyle(color: CoresPkLots.textoSecundario),
                    )
                  else ...[
                    const Text(
                      'Crie a senha da sua conta (mínimo de 8 caracteres, com letras e números).',
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
                      onFieldSubmitted: (_) => _aceitar(),
                    ),
                  ],
                  if (_erro != null) ...[
                    const SizedBox(height: 14),
                    Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                  ],
                  const SizedBox(height: 22),
                  FilledButton(
                    onPressed: _enviando ? null : _aceitar,
                    child: _enviando
                        ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : Text(convite.contaExistente ? 'CONFIRMAR ACESSO' : 'ATIVAR MEU ACESSO'),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Não é você? Ignore este convite: nada é liberado sem a confirmação.',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave),
                  ),
                ],
              ),
            ),
    );
  }
}

class _Mensagem extends StatelessWidget {
  const _Mensagem({
    required this.icone,
    required this.cor,
    required this.titulo,
    required this.texto,
    required this.botao,
    required this.aoPressionar,
  });

  final IconData icone;
  final Color cor;
  final String titulo;
  final String texto;
  final String botao;
  final VoidCallback aoPressionar;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Icon(icone, size: 48, color: cor),
      const SizedBox(height: 12),
      Text(
        titulo,
        textAlign: TextAlign.center,
        style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
      ),
      const SizedBox(height: 8),
      Text(texto, textAlign: TextAlign.center),
      const SizedBox(height: 20),
      FilledButton(onPressed: aoPressionar, child: Text(botao)),
    ],
  );
}
