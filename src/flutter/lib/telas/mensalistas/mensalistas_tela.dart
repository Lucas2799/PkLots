import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/situacao_mensalista.dart';
import 'formulario_mensalista.dart';
import 'pagamento_dialogo.dart';

/// Mensalistas do pátio selecionado
class MensalistasTela extends StatefulWidget {
  const MensalistasTela({super.key});

  @override
  State<MensalistasTela> createState() => _MensalistasTelaState();
}

class _MensalistasTelaState extends State<MensalistasTela> {
  final _busca = TextEditingController();
  Timer? _atraso;
  String? _filtro; // null = todos os ativos e inativos
  List<Mensalista>? _mensalistas;
  String? _erro;

  static const _filtros = [
    (null, 'Todos'),
    (Mensalista.emDia, 'Em dia'),
    (Mensalista.vencendo, 'Vencendo'),
    (Mensalista.vencido, 'Vencidos'),
    (Mensalista.inativo, 'Inativos'),
  ];

  ClienteApi get _api => context.read<ClienteApi>();
  double get _valorMensalidade => context.read<GestaoProvider>().dashboard?.mensalistas?.valorMensalidade ?? 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  @override
  void dispose() {
    _atraso?.cancel();
    _busca.dispose();
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final lista = await _api.mensalistas(situacao: _filtro, busca: _busca.text);
      if (mounted) {
        setState(() {
          _mensalistas = lista;
          _erro = null;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  void _buscarComAtraso(String _) {
    _atraso?.cancel();
    _atraso = Timer(const Duration(milliseconds: 350), _carregar);
  }

  Future<void> _novo() async {
    final criado = await mostrarFormularioMensalista(context);
    if (criado == null || !mounted) return;
    await _carregar();
    if (!mounted) return;
    mostrarAviso(context, '${criado.nome} cadastrado. Registre o pagamento para liberar a entrada como mensalista.');
    await _abrir(criado);
  }

  Future<void> _abrir(Mensalista mensalista) async {
    await Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => _DetalheMensalista(mensalista: mensalista, valorMensalidade: _valorMensalidade),
    ));
    await _carregar();
  }

  @override
  Widget build(BuildContext context) {
    final patio = context.watch<GestaoProvider>().patioAtual;
    final lista = _mensalistas;

    return Scaffold(
      appBar: AppBar(
        title: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Mensalistas'),
          if (patio != null) Text(patio.nome, style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
        ]),
        actions: [IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: _carregar)],
      ),
      // Novo cadastro só se o plano incluir mensalistas (os já cadastrados seguem visíveis)
      floatingActionButton: context.watch<GestaoProvider>().temRecurso(Recursos.mensalistas)
          ? FloatingActionButton.extended(
              onPressed: _novo,
              backgroundColor: CoresPkLots.azulMarinho,
              foregroundColor: Colors.white,
              icon: const Icon(Icons.person_add_alt),
              label: const Text('Novo mensalista'),
            )
          : null,
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 820),
          child: RefreshIndicator(
            onRefresh: _carregar,
            child: ListView(padding: const EdgeInsets.fromLTRB(16, 16, 16, 96), children: [
              TextField(
                controller: _busca,
                decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Buscar por nome ou placa'),
                onChanged: _buscarComAtraso,
              ),
              const SizedBox(height: 12),
              Wrap(spacing: 8, runSpacing: 8, children: [
                for (final (valor, rotulo) in _filtros)
                  ChoiceChip(
                    label: Text(rotulo),
                    selected: _filtro == valor,
                    onSelected: (_) {
                      setState(() => _filtro = valor);
                      _carregar();
                    },
                  ),
              ]),
              const SizedBox(height: 16),
              if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
              if (lista == null && _erro == null) const Center(child: CircularProgressIndicator()),
              if (lista != null && lista.isEmpty)
                const Padding(
                  padding: EdgeInsets.all(32),
                  child: Text('Nenhum mensalista encontrado.',
                      textAlign: TextAlign.center, style: TextStyle(color: CoresPkLots.textoSecundario)),
                ),
              if (lista != null && lista.isNotEmpty)
                Card(
                  child: Column(children: [
                    for (final (i, m) in lista.indexed) ...[
                      if (i > 0) const Divider(height: 1),
                      ListTile(
                        leading: CircleAvatar(
                          backgroundColor: m.ativo ? CoresPkLots.azulMarinho : CoresPkLots.borda,
                          foregroundColor: m.ativo ? Colors.white : CoresPkLots.textoSuave,
                          child: Text(m.nome.isEmpty ? '?' : m.nome[0].toUpperCase()),
                        ),
                        title: Row(children: [
                          Flexible(
                            child: Text(m.nome,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                          ),
                          if (m.estacionado) ...[
                            const SizedBox(width: 6),
                            const Tooltip(
                              message: 'Estacionado agora',
                              child: Icon(Icons.local_parking, size: 16, color: CoresPkLots.azulMarinho),
                            ),
                          ],
                        ]),
                        subtitle: Text([
                          m.placas.map(Formatadores.placa).join(', '),
                          if (m.validoAte != null) 'até ${Formatadores.data(m.validoAte!)}',
                        ].join(' · ')),
                        trailing: EtiquetaMensalista(mensalista: m),
                        onTap: () => _abrir(m),
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

/// Dados, situação e pagamentos de um mensalista
class _DetalheMensalista extends StatefulWidget {
  const _DetalheMensalista({required this.mensalista, required this.valorMensalidade});

  final Mensalista mensalista;
  final double valorMensalidade;

  @override
  State<_DetalheMensalista> createState() => _DetalheMensalistaState();
}

class _DetalheMensalistaState extends State<_DetalheMensalista> {
  late Mensalista _mensalista = widget.mensalista;
  List<PagamentoMensalidade>? _pagamentos;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregarPagamentos());
  }

  Future<void> _carregarPagamentos() async {
    try {
      final pagamentos = await context.read<ClienteApi>().pagamentosMensalista(_mensalista.id);
      if (mounted) setState(() => _pagamentos = pagamentos);
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _pagar() async {
    final atualizado = await mostrarRegistroPagamento(context, _mensalista, widget.valorMensalidade);
    if (atualizado == null || !mounted) return;
    setState(() => _mensalista = atualizado);
    mostrarAviso(context, 'Pagamento registrado. Válido até ${Formatadores.data(atualizado.validoAte!)}.');
    await _carregarPagamentos();
  }

  Future<void> _reenviarConvite() async {
    try {
      final atualizado = await context.read<ClienteApi>().reenviarConvite(_mensalista.id);
      if (!mounted) return;
      setState(() => _mensalista = atualizado);
      mostrarAviso(context, 'Convite reenviado para ${atualizado.email}');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _editar() async {
    final atualizado = await mostrarFormularioMensalista(context, mensalista: _mensalista);
    if (atualizado != null && mounted) setState(() => _mensalista = atualizado);
  }

  @override
  Widget build(BuildContext context) {
    final m = _mensalista;
    final pagamentos = _pagamentos;

    return Scaffold(
      appBar: AppBar(
        title: Text(m.nome),
        actions: [IconButton(tooltip: 'Editar', icon: const Icon(Icons.edit_outlined), onPressed: _editar)],
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 640),
          child: ListView(padding: const EdgeInsets.all(16), children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  Row(children: [
                    const Expanded(child: TituloSecao('Mensalidade', icone: Icons.card_membership_outlined)),
                    EtiquetaMensalista(mensalista: m),
                  ]),
                  Text(
                    m.validoAte == null ? 'Nenhum pagamento registrado' : 'Válida até ${Formatadores.data(m.validoAte!)}',
                    style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                  ),
                  if (m.situacao == Mensalista.vencido)
                    const Padding(
                      padding: EdgeInsets.only(top: 4),
                      child: Text('Enquanto estiver vencida, entra e paga como avulso.',
                          style: TextStyle(color: CoresPkLots.erro)),
                    ),
                  const SizedBox(height: 16),
                  FilledButton.icon(
                    onPressed: m.ativo ? _pagar : null,
                    icon: const Icon(Icons.payments_outlined),
                    label: const Text('Registrar pagamento'),
                  ),
                  if (!m.ativo)
                    const Padding(
                      padding: EdgeInsets.only(top: 6),
                      child: Text('Mensalista inativo: reative em Editar para registrar pagamentos.',
                          textAlign: TextAlign.center, style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                    ),
                ]),
              ),
            ),
            const SizedBox(height: 16),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  const TituloSecao('Dados', icone: Icons.person_outline),
                  Wrap(spacing: 8, runSpacing: 8, children: [
                    for (final placa in m.placas)
                      Chip(avatar: const Icon(Icons.directions_car, size: 18), label: Text(Formatadores.placa(placa))),
                  ]),
                  const SizedBox(height: 8),
                  if (m.telefone != null) Text('Telefone: ${m.telefone}'),
                  if (m.documento != null) Text('Documento: ${m.documento}'),
                  if (m.observacoes != null) Text('Observações: ${m.observacoes}'),
                  if (m.estacionado)
                    const Padding(
                      padding: EdgeInsets.only(top: 6),
                      child: Text('Estacionado agora', style: TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
                    ),
                ]),
              ),
            ),
            const SizedBox(height: 16),
            _AcessoApp(mensalista: m, aoReenviar: _reenviarConvite),
            const SizedBox(height: 16),
            Card(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
                child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  const TituloSecao('Pagamentos', icone: Icons.receipt_long_outlined),
                  if (pagamentos == null) const Center(child: CircularProgressIndicator()),
                  if (pagamentos != null && pagamentos.isEmpty)
                    const Padding(
                      padding: EdgeInsets.only(bottom: 12),
                      child: Text('Nenhum pagamento ainda.', style: TextStyle(color: CoresPkLots.textoSecundario)),
                    ),
                  for (final p in pagamentos ?? <PagamentoMensalidade>[])
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: const CircleAvatar(
                        backgroundColor: CoresPkLots.fundo,
                        child: Icon(Icons.check, color: CoresPkLots.disponivel),
                      ),
                      title: Text(Formatadores.moeda(p.valor),
                          style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                      subtitle: Text([
                        '${Formatadores.data(p.validoDe)} a ${Formatadores.data(p.validoAte)}',
                        PagamentoMensalidade.formas[p.formaPagamento] ?? p.formaPagamento,
                        ?p.observacao,
                      ].join(' · ')),
                      trailing: Text(Formatadores.dataCurta(p.pagoEm), style: const TextStyle(color: CoresPkLots.textoSuave)),
                    ),
                ]),
              ),
            ),
          ]),
        ),
      ),
    );
  }
}

/// Acesso do mensalista ao app: sem e-mail, convite pendente (com reenvio) ou ativo
class _AcessoApp extends StatelessWidget {
  const _AcessoApp({required this.mensalista, required this.aoReenviar});

  final Mensalista mensalista;
  final VoidCallback aoReenviar;

  @override
  Widget build(BuildContext context) {
    final m = mensalista;
    final (icone, cor, texto) = switch (m.acessoApp) {
      Mensalista.acessoAtivo => (Icons.verified_user_outlined, CoresPkLots.disponivel, 'Acesso ativo: ${m.email}'),
      Mensalista.convitePendente =>
        (Icons.mark_email_unread_outlined, CoresPkLots.ambarEscuro, 'Convite enviado para ${m.email}, ainda não aceito'),
      _ => (Icons.no_accounts_outlined, CoresPkLots.textoSuave, 'Sem acesso ao app. Informe um e-mail em Editar para enviar o convite.'),
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          const TituloSecao('Acesso ao app', icone: Icons.phone_iphone),
          Row(children: [
            Icon(icone, color: cor),
            const SizedBox(width: 10),
            Expanded(child: Text(texto, style: const TextStyle(color: CoresPkLots.azulMarinho))),
          ]),
          if (m.acessoApp == Mensalista.convitePendente && m.ativo) ...[
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                onPressed: aoReenviar,
                icon: const Icon(Icons.forward_to_inbox_outlined),
                label: const Text('Reenviar convite'),
              ),
            ),
          ],
        ]),
      ),
    );
  }
}
