import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../util/validadores.dart';
import '../../widgets/estados_tela.dart';
import 'perfis.dart';

/// Usuários do pátio: criar, alterar perfil, ativar/desativar e redefinir senha
class AbaUsuarios extends StatelessWidget {
  const AbaUsuarios({super.key});

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<AdministracaoProvider>();
    final meuEmail = context.watch<SessaoProvider>().emailUsuario;

    return ListView(padding: const EdgeInsets.all(16), children: [
      Row(children: [
        Expanded(
          child: Text('${estado.usuarios.length} usuários',
              style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho, fontSize: 16)),
        ),
        SizedBox(
          width: 180,
          child: FilledButton.icon(
            onPressed: () => showDialog(context: context, builder: (_) => const _FormularioUsuario()),
            icon: const Icon(Icons.person_add_alt),
            label: const Text('Novo usuário'),
          ),
        ),
      ]),
      const SizedBox(height: 16),
      Card(
        child: Column(children: [
          for (final (i, usuario) in estado.usuarios.indexed) ...[
            if (i > 0) const Divider(height: 1),
            ListTile(
              leading: CircleAvatar(
                backgroundColor: usuario.ativo ? CoresPkLots.azulMarinho : CoresPkLots.borda,
                foregroundColor: usuario.ativo ? Colors.white : CoresPkLots.textoSuave,
                child: Text(usuario.nomeCompleto.isEmpty ? '?' : usuario.nomeCompleto[0].toUpperCase()),
              ),
              title: Row(children: [
                Flexible(
                  child: Text(
                    usuario.nomeCompleto + (usuario.email == meuEmail ? ' (você)' : ''),
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: usuario.ativo ? CoresPkLots.azulMarinho : CoresPkLots.textoSuave,
                    ),
                  ),
                ),
              ]),
              subtitle: Text([
                usuario.email,
                if (usuario.perfil == 'operador')
                  estado.patios.where((p) => usuario.patiosIds.contains(p.id)).map((p) => p.nome).join(', '),
                if (!usuario.ativo) 'Inativo',
                if (usuario.ultimoLogin != null) 'Último acesso ${Formatadores.dataHora(usuario.ultimoLogin!)}',
              ].join(' · ')),
              trailing: EtiquetaPerfil(perfil: usuario.perfil, ativo: usuario.ativo),
              onTap: () => showDialog(
                context: context,
                builder: (_) => _FormularioUsuario(usuario: usuario, ehVoce: usuario.email == meuEmail),
              ),
            ),
          ],
        ]),
      ),
    ]);
  }
}

/// Criação (usuario == null) ou edição de usuário
class _FormularioUsuario extends StatefulWidget {
  const _FormularioUsuario({this.usuario, this.ehVoce = false});

  final UsuarioAdmin? usuario;
  final bool ehVoce;

  @override
  State<_FormularioUsuario> createState() => _FormularioUsuarioState();
}

class _FormularioUsuarioState extends State<_FormularioUsuario> {
  final _formulario = GlobalKey<FormState>();
  late final _nome = TextEditingController(text: widget.usuario?.nomeCompleto);
  late final _email = TextEditingController(text: widget.usuario?.email);
  final _senha = TextEditingController();
  late String _perfil = widget.usuario?.perfil ?? 'operador';
  late final Set<String> _patios = {...?widget.usuario?.patiosIds};
  late bool _ativo = widget.usuario?.ativo ?? true;
  bool _enviando = false;
  bool _novaSenha = false;
  String? _erro;

  bool get _criando => widget.usuario == null;

  @override
  void dispose() {
    for (final c in [_nome, _email, _senha]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    if (_perfil == 'operador' && _patios.isEmpty) {
      setState(() => _erro = 'Selecione pelo menos um pátio para o operador.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    final estado = context.read<AdministracaoProvider>();
    final patios = _perfil == 'operador' ? _patios.toList() : <String>[];
    try {
      if (_criando) {
        await estado.criarUsuario(_email.text.trim(), _nome.text.trim(), _senha.text, _perfil, patios);
      } else {
        await estado.atualizarUsuario(widget.usuario!.id, _nome.text.trim(), _perfil, _ativo, patios);
        if (_novaSenha) await estado.redefinirSenha(widget.usuario!.id, _senha.text);
      }
      if (mounted) {
        Navigator.of(context).pop();
        mostrarAviso(context, _criando ? 'Usuário criado.' : 'Usuário atualizado.');
      }
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text(_criando ? 'Novo usuário' : 'Editar usuário'),
        content: SizedBox(
          width: 400,
          child: Form(
            key: _formulario,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                TextFormField(
                  controller: _nome,
                  decoration: const InputDecoration(labelText: 'Nome completo'),
                  textCapitalization: TextCapitalization.words,
                  validator: (v) => Validadores.obrigatorio(v, 'o nome'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _email,
                  enabled: _criando,
                  decoration: const InputDecoration(labelText: 'E-mail'),
                  keyboardType: TextInputType.emailAddress,
                  validator: Validadores.email,
                ),
                const SizedBox(height: 16),
                const Text('Perfil', style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                RadioGroup<String>(
                  groupValue: _perfil,
                  onChanged: (valor) {
                    if (!widget.ehVoce && valor != null) setState(() => _perfil = valor);
                  },
                  child: Column(children: [
                    for (final perfil in UsuarioAdmin.perfis)
                      RadioListTile<String>(
                        value: perfil,
                        enabled: !widget.ehVoce,
                        contentPadding: EdgeInsets.zero,
                        dense: true,
                        secondary: Icon(InfoPerfil.de(perfil).icone),
                        title: Text(InfoPerfil.de(perfil).rotulo),
                        subtitle: Text(InfoPerfil.de(perfil).descricao),
                      ),
                  ]),
                ),
                if (_perfil == 'operador') ...[
                  const SizedBox(height: 8),
                  const Text('Pátios em que trabalha',
                      style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                  for (final patio in context.read<AdministracaoProvider>().patios.where((p) => p.ativo))
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      dense: true,
                      title: Text(patio.nome),
                      value: _patios.contains(patio.id),
                      onChanged: (marcado) =>
                          setState(() => marcado == true ? _patios.add(patio.id) : _patios.remove(patio.id)),
                    ),
                ],
                if (!_criando)
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Usuário ativo'),
                    subtitle: Text(widget.ehVoce
                        ? 'Você não pode desativar a própria conta'
                        : 'Inativo não consegue entrar e perde o acesso na hora'),
                    value: _ativo,
                    onChanged: widget.ehVoce ? null : (valor) => setState(() => _ativo = valor),
                  ),
                if (!_criando && !_novaSenha)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: () => setState(() => _novaSenha = true),
                      icon: const Icon(Icons.key_outlined),
                      label: const Text('Redefinir senha'),
                    ),
                  ),
                if (_criando || _novaSenha) ...[
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: _senha,
                    obscureText: true,
                    decoration: InputDecoration(
                      labelText: _criando ? 'Senha inicial' : 'Nova senha',
                      helperText: 'Mínimo de 8 caracteres',
                    ),
                    validator: Validadores.senha,
                  ),
                ],
                if (_erro != null) ...[
                  const SizedBox(height: 12),
                  Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                ],
              ]),
            ),
          ),
        ),
        actions: [
          TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(), child: const Text('Cancelar')),
          SizedBox(
            width: 120,
            child: FilledButton(
              onPressed: _enviando ? null : _salvar,
              child: _enviando
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Salvar'),
            ),
          ),
        ],
      );
}
