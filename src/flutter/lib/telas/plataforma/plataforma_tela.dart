import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../util/validadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/logo_pklots.dart';
import '../relatorios/relatorio_plataforma_tela.dart';
import 'assinatura_dialogo.dart';
import 'faturas_tela.dart';
import 'planos_tela.dart';
import '../troca_senha_tela.dart';

/// Área do superadmin: empresas clientes da plataforma
class PlataformaTela extends StatefulWidget {
  const PlataformaTela({super.key});

  @override
  State<PlataformaTela> createState() => _PlataformaTelaState();
}

class _PlataformaTelaState extends State<PlataformaTela> {
  List<EmpresaPlataforma>? _empresas;
  String? _erro;

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  Future<void> _carregar() async {
    try {
      final empresas = await _api.empresas();
      if (mounted) {
        setState(() {
          _empresas = empresas;
          _erro = null;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  Future<void> _alternarAtiva(EmpresaPlataforma empresa) async {
    final desativar = empresa.ativa;
    if (desativar) {
      final confirmou = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text('Desativar ${empresa.nome}?'),
          content: const Text('Todos os usuários da empresa perdem o acesso imediatamente e os pátios somem do app dos motoristas.'),
          actions: [
            TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
            TextButton(onPressed: () => Navigator.of(context).pop(true), child: const Text('Desativar')),
          ],
        ),
      );
      if (confirmou != true) return;
    }
    try {
      await _api.atualizarEmpresa(empresa.id, empresa.nome, empresa.documento, !empresa.ativa);
      await _carregar();
      if (mounted) mostrarAviso(context, desativar ? 'Empresa desativada.' : 'Empresa reativada.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _reenviarConvite(EmpresaPlataforma empresa) async {
    try {
      await _api.reenviarConviteEmpresa(empresa.id);
      if (mounted) mostrarAviso(context, 'Convite reenviado ao administrador de ${empresa.nome}.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _abrirAssinatura(EmpresaPlataforma empresa) async {
    final salvou = await showDialog<bool>(context: context, builder: (_) => AssinaturaDialogo(empresa: empresa));
    if (salvou != true) return;
    await _carregar();
    if (mounted) mostrarAviso(context, 'Assinatura de ${empresa.nome} atualizada.');
  }

  @override
  Widget build(BuildContext context) {
    final sessao = context.watch<SessaoProvider>();
    final empresas = _empresas;

    return Scaffold(
      appBar: AppBar(
        title: const Row(mainAxisSize: MainAxisSize.min, children: [
          MarcaPkLots(altura: 28),
          SizedBox(width: 12),
          Flexible(child: Text('· Plataforma', style: TextStyle(fontSize: 16, color: CoresPkLots.textoSecundario))),
        ]),
        actions: [
          IconButton(
            tooltip: 'Faturas',
            icon: const Icon(Icons.request_quote_outlined),
            onPressed: () => Navigator.of(context)
                .push(MaterialPageRoute(builder: (_) => FaturasTela(empresas: _empresas ?? const []))),
          ),
          IconButton(
            tooltip: 'Planos',
            icon: const Icon(Icons.workspace_premium_outlined),
            onPressed: () async {
              await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PlanosTela()));
              await _carregar();
            },
          ),
          IconButton(
            tooltip: 'Relatórios',
            icon: const Icon(Icons.insights_outlined),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const RelatorioPlataformaTela())),
          ),
          IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: _carregar),
          IconButton(
            tooltip: 'Alterar senha',
            icon: const Icon(Icons.key_outlined),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const TrocaSenhaTela())),
          ),
          IconButton(tooltip: 'Sair', icon: const Icon(Icons.logout), onPressed: sessao.sair),
        ],
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 820),
          child: RefreshIndicator(
            onRefresh: _carregar,
            child: ListView(padding: const EdgeInsets.all(16), children: [
              Row(children: [
                Expanded(
                  child: Text(empresas == null ? 'Empresas' : '${empresas.length} empresa(s)',
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                ),
                SizedBox(
                  width: 180,
                  child: FilledButton.icon(
                    onPressed: () async {
                      final criada = await showDialog<bool>(context: context, builder: (_) => const _NovaEmpresa());
                      if (criada == true) await _carregar();
                    },
                    icon: const Icon(Icons.add_business_outlined),
                    label: const Text('Nova empresa'),
                  ),
                ),
              ]),
              const SizedBox(height: 16),
              if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
              if (empresas == null && _erro == null) const Center(child: CircularProgressIndicator()),
              if (empresas != null)
                Card(
                  child: Column(children: [
                    for (final (i, empresa) in empresas.indexed) ...[
                      if (i > 0) const Divider(height: 1),
                      ListTile(
                        leading: CircleAvatar(
                          backgroundColor: empresa.ativa ? CoresPkLots.azulMarinho : CoresPkLots.borda,
                          foregroundColor: empresa.ativa ? Colors.white : CoresPkLots.textoSuave,
                          child: const Icon(Icons.business),
                        ),
                        title: Text(empresa.nome,
                            style: TextStyle(
                              fontWeight: FontWeight.w600,
                              color: empresa.ativa ? CoresPkLots.azulMarinho : CoresPkLots.textoSuave,
                            )),
                        subtitle: Text([
                          ?empresa.documento,
                          if (empresa.nomePlano != null) 'Plano ${empresa.nomePlano}',
                          if (empresa.situacaoCobranca == SituacaoCobranca.vencida) 'Fatura vencida',
                          if (empresa.situacaoCobranca == SituacaoCobranca.bloqueada) 'Bloqueio parcial (fatura vencida)',
                          '${empresa.patios} pátio(s)',
                          '${empresa.usuarios} usuário(s)',
                          'desde ${Formatadores.dataCurta(empresa.criadoEm)}',
                          if (!empresa.ativa) 'Desativada',
                          if (empresa.convitesPendentes > 0) 'Convite pendente',
                        ].join(' · ')),
                        trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                          if (empresa.ativa && empresa.convitesPendentes > 0)
                            IconButton(
                              tooltip: 'Reenviar convite do administrador',
                              icon: const Icon(Icons.forward_to_inbox_outlined),
                              onPressed: () => _reenviarConvite(empresa),
                            ),
                          IconButton(
                            tooltip: 'Assinatura',
                            icon: const Icon(Icons.receipt_long_outlined),
                            onPressed: () => _abrirAssinatura(empresa),
                          ),
                          Switch(
                            value: empresa.ativa,
                            onChanged: (_) => _alternarAtiva(empresa),
                          ),
                        ]),
                      ),
                    ],
                  ]),
                ),
            ]),
          ),
        ),
      ),
    );
  }
}

/// Cria a empresa com o primeiro pátio e o administrador
class _NovaEmpresa extends StatefulWidget {
  const _NovaEmpresa();

  @override
  State<_NovaEmpresa> createState() => _NovaEmpresaState();
}

class _NovaEmpresaState extends State<_NovaEmpresa> {
  final _formulario = GlobalKey<FormState>();
  final _nome = TextEditingController();
  final _documento = TextEditingController();
  final _patio = TextEditingController(text: 'Pátio Principal');
  final _vagas = TextEditingController(text: '30');
  final _nomeAdmin = TextEditingController();
  final _emailAdmin = TextEditingController();
  List<Plano>? _planos;
  String? _planoId;
  bool _enviando = false;
  String? _erro;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregarPlanos());
  }

  Future<void> _carregarPlanos() async {
    try {
      final planos = (await context.read<ClienteApi>().planos()).where((p) => p.ativo).toList();
      if (mounted) setState(() => _planos = planos);
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  @override
  void dispose() {
    for (final c in [_nome, _documento, _patio, _vagas, _nomeAdmin, _emailAdmin]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<ClienteApi>().criarEmpresa(
            nome: _nome.text.trim(),
            documento: _documento.text.trim().isEmpty ? null : _documento.text.trim(),
            nomePatio: _patio.text.trim(),
            totalVagas: int.parse(_vagas.text),
            nomeAdmin: _nomeAdmin.text.trim(),
            emailAdmin: _emailAdmin.text.trim(),
            planoId: _planoId,
          );
      if (mounted) {
        Navigator.of(context).pop(true);
        mostrarAviso(context, 'Empresa criada. Convite enviado para ${_emailAdmin.text.trim()}.');
      }
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Nova empresa'),
        content: SizedBox(
          width: 440,
          child: Form(
            key: _formulario,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                TextFormField(
                  controller: _nome,
                  autofocus: true,
                  decoration: const InputDecoration(labelText: 'Nome da empresa'),
                  validator: (v) => Validadores.obrigatorio(v, 'o nome'),
                ),
                const SizedBox(height: 12),
                TextFormField(controller: _documento, decoration: const InputDecoration(labelText: 'CNPJ/CPF (opcional)')),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _planoId,
                  isExpanded: true,
                  decoration: InputDecoration(labelText: 'Plano', helperText: _planos?.isEmpty == true ? 'Cadastre um plano antes' : null),
                  items: [
                    for (final p in _planos ?? <Plano>[])
                      DropdownMenuItem(value: p.id, child: Text('${p.nome} · ${Formatadores.moeda(p.precoPorPatio)}/pátio')),
                  ],
                  onChanged: (id) => setState(() => _planoId = id),
                  validator: (v) => v == null ? 'Escolha o plano' : null,
                ),
                const SizedBox(height: 20),
                const TituloSecao('Primeiro pátio', icone: Icons.local_parking),
                Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Expanded(
                    flex: 3,
                    child: TextFormField(
                      controller: _patio,
                      decoration: const InputDecoration(labelText: 'Nome do pátio'),
                      validator: (v) => Validadores.obrigatorio(v, 'o pátio'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: _vagas,
                      decoration: const InputDecoration(labelText: 'Vagas'),
                      keyboardType: TextInputType.number,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      validator: (v) {
                        final n = int.tryParse(v ?? '');
                        return n == null || n < 1 || n > 260 ? '1 a 260' : null;
                      },
                    ),
                  ),
                ]),
                const SizedBox(height: 20),
                const TituloSecao('Administrador da empresa', icone: Icons.admin_panel_settings_outlined),
                TextFormField(
                  controller: _nomeAdmin,
                  decoration: const InputDecoration(labelText: 'Nome completo'),
                  validator: (v) => Validadores.obrigatorio(v, 'o nome'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _emailAdmin,
                  decoration: const InputDecoration(labelText: 'E-mail (login)'),
                  keyboardType: TextInputType.emailAddress,
                  validator: Validadores.email,
                ),
                const SizedBox(height: 8),
                const Text('O administrador recebe um convite por e-mail e cria a própria senha.',
                    style: TextStyle(fontSize: 13, color: CoresPkLots.textoSecundario)),
                if (_erro != null) ...[
                  const SizedBox(height: 12),
                  Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                ],
              ]),
            ),
          ),
        ),
        actions: [
          TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
          SizedBox(
            width: 140,
            child: FilledButton(
              onPressed: _enviando ? null : _salvar,
              child: _enviando
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Criar empresa'),
            ),
          ),
        ],
      );
}
