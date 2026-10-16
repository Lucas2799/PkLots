import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import '../relatorios/componentes_relatorio.dart';

/// Auditoria: quem fez o quê e quando. Admin vê a própria empresa; superadmin vê todas e também os logs técnicos da API.
class AuditoriaTela extends StatelessWidget {
  const AuditoriaTela({super.key, required this.superAdmin});

  final bool superAdmin;

  @override
  Widget build(BuildContext context) {
    if (!superAdmin) {
      return Scaffold(appBar: AppBar(title: const Text('Auditoria')), body: const _AbaAuditoria(superAdmin: false));
    }
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Auditoria'),
          bottom: const TabBar(tabs: [
            Tab(icon: Icon(Icons.fact_check_outlined), text: 'Ações dos usuários'),
            Tab(icon: Icon(Icons.terminal), text: 'Logs do sistema'),
          ]),
        ),
        body: const TabBarView(children: [_AbaAuditoria(superAdmin: true), _AbaLogsSistema()]),
      ),
    );
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Ações dos usuários

class _AbaAuditoria extends StatefulWidget {
  const _AbaAuditoria({required this.superAdmin});

  final bool superAdmin;

  @override
  State<_AbaAuditoria> createState() => _AbaAuditoriaState();
}

class _AbaAuditoriaState extends State<_AbaAuditoria> with AutomaticKeepAliveClientMixin {
  var _atalho = AtalhoPeriodo.seteDias;
  late PeriodoRelatorio _periodo;
  String? _empresaId, _patioId, _categoria, _situacao, _usuarioId;
  final _placa = TextEditingController();
  OpcoesAuditoria? _opcoes;

  final List<RegistroAuditoria> _itens = [];
  PaginaAuditoria? _ultima;
  String? _erro;
  bool _carregando = false;
  int _consulta = 0; // descarta respostas de filtros já trocados

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    final agora = DateTime.now();
    _periodo = _atalho.periodo(DateTime(agora.year, agora.month, agora.day))!;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _carregarOpcoes();
      _carregar();
    });
  }

  @override
  void dispose() {
    _placa.dispose();
    super.dispose();
  }

  Future<void> _carregarOpcoes() async {
    try {
      final opcoes = await _api.opcoesAuditoria(empresaId: _empresaId);
      if (mounted) setState(() => _opcoes = opcoes);
    } on ExcecaoApi {
      // Sem as listas, os filtros por pátio/usuário ficam escondidos
    }
  }

  FiltroAuditoria get _filtro => FiltroAuditoria(
        periodo: _periodo,
        empresaId: _empresaId,
        patioId: _patioId,
        categoria: _categoria,
        situacao: _situacao,
        placa: _placa.text,
        usuarioId: _usuarioId,
      );

  Future<void> _carregar({bool mais = false}) async {
    final consulta = ++_consulta;
    setState(() {
      _carregando = true;
      _erro = null;
      if (!mais) {
        _itens.clear();
        _ultima = null;
      }
    });
    try {
      final pagina = await _api.auditoria(_filtro, pagina: mais ? (_ultima?.pagina ?? 0) + 1 : 1);
      if (mounted && consulta == _consulta) {
        setState(() {
          _itens.addAll(pagina.itens);
          _ultima = pagina;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted && consulta == _consulta) setState(() => _erro = e.mensagem);
    } finally {
      if (mounted && consulta == _consulta) setState(() => _carregando = false);
    }
  }

  void _mudar(VoidCallback mudanca) {
    setState(mudanca);
    _carregar();
  }

  Widget _filtroLista(String rotulo, IconData icone, String? valor, String todos, List<OpcaoFiltro> opcoes, ValueChanged<String?> aoMudar) =>
      SizedBox(
        width: 240,
        child: DropdownButtonFormField<String?>(
          key: ValueKey((rotulo, opcoes.length, valor)),
          initialValue: valor,
          isExpanded: true,
          decoration: InputDecoration(labelText: rotulo, prefixIcon: Icon(icone)),
          items: [
            DropdownMenuItem(value: null, child: Text(todos)),
            for (final o in opcoes) DropdownMenuItem(value: o.id, child: Text(o.nome, overflow: TextOverflow.ellipsis)),
          ],
          onChanged: aoMudar,
        ),
      );

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final opcoes = _opcoes;
    final ultima = _ultima;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 960),
        child: RefreshIndicator(
          onRefresh: _carregar,
          child: ListView(padding: const EdgeInsets.all(16), children: [
            SeletorPeriodo(
              atalho: _atalho,
              periodo: _periodo,
              aoMudar: (atalho, periodo) => _mudar(() {
                _atalho = atalho;
                _periodo = periodo;
              }),
            ),
            const SizedBox(height: 12),
            Wrap(spacing: 12, runSpacing: 12, children: [
              if (widget.superAdmin && opcoes != null)
                _filtroLista('Empresa', Icons.business_outlined, _empresaId, 'Todas as empresas', opcoes.empresas, (id) {
                  _mudar(() {
                    _empresaId = id;
                    _patioId = null;
                    _usuarioId = null;
                  });
                  _carregarOpcoes();
                }),
              if (opcoes != null && opcoes.patios.length > 1)
                _filtroLista('Pátio', Icons.store_mall_directory_outlined, _patioId, 'Todos os pátios', opcoes.patios,
                    (id) => _mudar(() => _patioId = id)),
              if (opcoes != null)
                _filtroLista('Tipo', Icons.category_outlined, _categoria, 'Todos os tipos', opcoes.categorias,
                    (c) => _mudar(() => _categoria = c)),
              if (opcoes != null && opcoes.usuarios.isNotEmpty)
                _filtroLista('Usuário', Icons.person_outline, _usuarioId, 'Todos os usuários', opcoes.usuarios,
                    (id) => _mudar(() => _usuarioId = id)),
              _filtroLista('Situação', Icons.flag_outlined, _situacao, 'Todas', [
                OpcaoFiltro(id: RegistroAuditoria.sucesso, nome: 'Sucesso'),
                OpcaoFiltro(id: RegistroAuditoria.alerta, nome: 'Alerta'),
                OpcaoFiltro(id: RegistroAuditoria.erro, nome: 'Erro'),
              ], (s) => _mudar(() => _situacao = s)),
              SizedBox(
                width: 240,
                child: TextField(
                  controller: _placa,
                  textCapitalization: TextCapitalization.characters,
                  textInputAction: TextInputAction.search,
                  decoration: InputDecoration(
                    labelText: 'Placa',
                    prefixIcon: const Icon(Icons.search),
                    suffixIcon: IconButton(tooltip: 'Buscar placa', icon: const Icon(Icons.arrow_forward), onPressed: _carregar),
                  ),
                  onSubmitted: (_) => _carregar(),
                ),
              ),
            ]),
            const SizedBox(height: 16),
            if (ultima != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Text(
                  ultima.total == 0 ? 'Nada registrado com esses filtros.' : '${ultima.total} registro(s), mais recentes primeiro',
                  style: const TextStyle(color: CoresPkLots.textoSecundario),
                ),
              ),
            if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
            for (final item in _itens) _ItemAuditoria(item: item, mostrarEmpresa: widget.superAdmin && _empresaId == null),
            if (_carregando) const Padding(padding: EdgeInsets.all(24), child: Center(child: CircularProgressIndicator())),
            if (!_carregando && ultima != null && ultima.temMais)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Center(
                  child: OutlinedButton.icon(
                    onPressed: () => _carregar(mais: true),
                    icon: const Icon(Icons.expand_more),
                    label: Text('Carregar mais (${_itens.length} de ${ultima.total})'),
                  ),
                ),
              ),
          ]),
        ),
      ),
    );
  }
}

class _ItemAuditoria extends StatelessWidget {
  const _ItemAuditoria({required this.item, required this.mostrarEmpresa});

  final RegistroAuditoria item;
  final bool mostrarEmpresa;

  static (IconData, Color) _visual(String situacao) => switch (situacao) {
        RegistroAuditoria.erro => (Icons.error_outline, CoresPkLots.erro),
        RegistroAuditoria.alerta => (Icons.warning_amber_rounded, CoresPkLots.manutencao),
        _ => (Icons.check_circle_outline, CoresPkLots.disponivel),
      };

  @override
  Widget build(BuildContext context) {
    final (icone, cor) = _visual(item.situacao);
    final titulo = item.placa == null ? item.descricao : '${item.descricao} · ${Formatadores.placa(item.placa!)}';
    final local = [if (mostrarEmpresa && item.empresa != null) item.empresa!, item.patio].join(' · ');

    return Card(
      child: ExpansionTile(
        leading: Icon(icone, color: cor),
        title: Text(titulo, style: const TextStyle(fontWeight: FontWeight.w600)),
        subtitle: Text('${Formatadores.dataHoraCompleta(item.criadoEm)} · ${item.usuario}\n$local',
            style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
        expandedCrossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (item.detalhes.isEmpty)
            const Text('Sem detalhes adicionais.', style: TextStyle(color: CoresPkLots.textoSuave)),
          for (final d in item.detalhes)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 3),
              child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                SizedBox(width: 170, child: Text(d.rotulo, style: const TextStyle(color: CoresPkLots.textoSecundario))),
                Expanded(child: SelectableText(d.valor)),
              ]),
            ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Logs do sistema (superadmin): requisições, avisos e erros da API

enum _PeriodoLog {
  hoje('Hoje'),
  ontem('Ontem'),
  seteDias('7 dias');

  const _PeriodoLog(this.rotulo);
  final String rotulo;

  PeriodoRelatorio periodo(DateTime d) => switch (this) {
        hoje => PeriodoRelatorio(d, d),
        ontem => PeriodoRelatorio(DateTime(d.year, d.month, d.day - 1), DateTime(d.year, d.month, d.day - 1)),
        seteDias => PeriodoRelatorio(DateTime(d.year, d.month, d.day - 6), d),
      };
}

class _AbaLogsSistema extends StatefulWidget {
  const _AbaLogsSistema();

  @override
  State<_AbaLogsSistema> createState() => _AbaLogsSistemaState();
}

class _AbaLogsSistemaState extends State<_AbaLogsSistema> with AutomaticKeepAliveClientMixin {
  static const niveis = [('Information', 'Tudo'), ('Warning', 'Avisos e erros'), ('Error', 'Só erros')];
  static final _hora = DateFormat('dd/MM HH:mm:ss', 'pt_BR');

  var _periodoLog = _PeriodoLog.hoje;
  String _nivel = 'Warning';
  final _texto = TextEditingController();

  final List<RegistroLogSistema> _itens = [];
  PaginaLogsSistema? _ultima;
  String? _erro;
  bool _carregando = false;
  int _consulta = 0;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  @override
  void dispose() {
    _texto.dispose();
    super.dispose();
  }

  Future<void> _carregar({bool mais = false}) async {
    final consulta = ++_consulta;
    setState(() {
      _carregando = true;
      _erro = null;
      if (!mais) {
        _itens.clear();
        _ultima = null;
      }
    });
    try {
      final agora = DateTime.now();
      final pagina = await context.read<ClienteApi>().logsSistema(_periodoLog.periodo(DateTime(agora.year, agora.month, agora.day)),
          nivel: _nivel, texto: _texto.text, pagina: mais ? (_ultima?.pagina ?? 0) + 1 : 1);
      if (mounted && consulta == _consulta) {
        setState(() {
          _itens.addAll(pagina.itens);
          _ultima = pagina;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted && consulta == _consulta) setState(() => _erro = e.mensagem);
    } finally {
      if (mounted && consulta == _consulta) setState(() => _carregando = false);
    }
  }

  static Color _cor(String nivel) => switch (nivel) {
        'Error' || 'Fatal' => CoresPkLots.erro,
        'Warning' => CoresPkLots.manutencao,
        _ => CoresPkLots.textoSuave,
      };

  static String _rotuloNivel(String nivel) => switch (nivel) {
        'Error' => 'ERRO',
        'Fatal' => 'FATAL',
        'Warning' => 'AVISO',
        'Information' => 'INFO',
        _ => nivel.toUpperCase(),
      };

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final ultima = _ultima;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 960),
        child: RefreshIndicator(
          onRefresh: _carregar,
          child: ListView(padding: const EdgeInsets.all(16), children: [
            const Text(
              'Registro técnico da API: requisições, avisos e erros (com o detalhe da falha). '
              'Útil para investigar um problema relatado por um cliente. Guardado por 31 dias.',
              style: TextStyle(color: CoresPkLots.textoSecundario),
            ),
            const SizedBox(height: 12),
            Wrap(spacing: 12, runSpacing: 12, crossAxisAlignment: WrapCrossAlignment.center, children: [
              Wrap(spacing: 8, children: [
                for (final p in _PeriodoLog.values)
                  ChoiceChip(
                    label: Text(p.rotulo),
                    selected: p == _periodoLog,
                    onSelected: (_) {
                      setState(() => _periodoLog = p);
                      _carregar();
                    },
                  ),
              ]),
              SegmentedButton<String>(
                segments: [for (final (valor, rotulo) in niveis) ButtonSegment(value: valor, label: Text(rotulo))],
                selected: {_nivel},
                onSelectionChanged: (s) {
                  setState(() => _nivel = s.first);
                  _carregar();
                },
              ),
              SizedBox(
                width: 280,
                child: TextField(
                  controller: _texto,
                  textInputAction: TextInputAction.search,
                  decoration: InputDecoration(
                    labelText: 'Buscar no log',
                    hintText: 'Ex.: /api/saidas, e-mail, timeout',
                    prefixIcon: const Icon(Icons.search),
                    suffixIcon: IconButton(tooltip: 'Buscar no log', icon: const Icon(Icons.arrow_forward), onPressed: _carregar),
                  ),
                  onSubmitted: (_) => _carregar(),
                ),
              ),
            ]),
            const SizedBox(height: 16),
            if (ultima != null && !ultima.disponivel)
              const Text('Nenhum arquivo de log encontrado no servidor.', style: TextStyle(color: CoresPkLots.textoSecundario))
            else if (ultima != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Text(ultima.total == 0 ? 'Nada registrado com esses filtros.' : '${ultima.total} registro(s), mais recentes primeiro',
                    style: const TextStyle(color: CoresPkLots.textoSecundario)),
              ),
            if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
            for (final item in _itens)
              Card(
                child: ExpansionTile(
                  leading: Container(
                    width: 52,
                    padding: const EdgeInsets.symmetric(vertical: 3),
                    decoration: BoxDecoration(color: _cor(item.nivel).withValues(alpha: 0.12), borderRadius: BorderRadius.circular(6)),
                    child: Text(_rotuloNivel(item.nivel),
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: _cor(item.nivel))),
                  ),
                  title: Text(item.mensagem, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 14)),
                  subtitle: Text(
                    [_hora.format(item.quando.toLocal()), if (item.origem != null) item.origem!.split('.').last].join(' · '),
                    style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario),
                  ),
                  childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                  expandedCrossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    SelectableText(item.mensagem),
                    if (item.origem != null) ...[
                      const SizedBox(height: 6),
                      Text('Origem: ${item.origem}', style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                    ],
                    if (item.excecao != null) ...[
                      const SizedBox(height: 8),
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(color: CoresPkLots.fundo, borderRadius: BorderRadius.circular(6)),
                        child: SelectableText(item.excecao!, style: const TextStyle(fontFamily: 'monospace', fontSize: 12)),
                      ),
                    ],
                  ],
                ),
              ),
            if (_carregando) const Padding(padding: EdgeInsets.all(24), child: Center(child: CircularProgressIndicator())),
            if (!_carregando && ultima != null && ultima.temMais)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Center(
                  child: OutlinedButton.icon(
                    onPressed: () => _carregar(mais: true),
                    icon: const Icon(Icons.expand_more),
                    label: Text('Carregar mais (${_itens.length} de ${ultima.total})'),
                  ),
                ),
              ),
          ]),
        ),
      ),
    );
  }
}
