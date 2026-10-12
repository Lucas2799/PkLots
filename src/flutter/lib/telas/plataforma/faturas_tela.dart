import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/caixa_dialogos.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/fatura_widgets.dart';

/// Cobrança do aluguel do PkLots (superadmin): faturas das empresas, baixa manual, avulsas e chave PIX
class FaturasTela extends StatefulWidget {
  const FaturasTela({super.key, required this.empresas});

  /// Para o lançamento avulso
  final List<EmpresaPlataforma> empresas;

  @override
  State<FaturasTela> createState() => _FaturasTelaState();
}

class _FaturasTelaState extends State<FaturasTela> {
  static const _filtros = [
    (null, 'Todas'),
    (Fatura.aberta, 'Em aberto'),
    (Fatura.vencida, 'Vencidas'),
    (Fatura.paga, 'Pagas'),
    (Fatura.cancelada, 'Canceladas'),
  ];

  String? _filtro = Fatura.aberta;
  List<Fatura>? _faturas;
  String? _erro;

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  Future<void> _carregar() async {
    try {
      final faturas = await _api.faturas(situacao: _filtro);
      if (mounted) {
        setState(() {
          _faturas = faturas;
          _erro = null;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  Future<void> _executar(Future<void> Function() acao, String sucesso) async {
    try {
      await acao();
      await _carregar();
      if (mounted) mostrarAviso(context, sucesso);
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _emitir() async {
    try {
      final emitidas = await _api.emitirFaturas();
      await _carregar();
      if (mounted) {
        mostrarAviso(context, emitidas == 0 ? 'Nenhuma mensalidade a emitir agora.' : '$emitidas fatura(s) emitida(s) e enviada(s) por e-mail.');
      }
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _baixar(Fatura fatura) async {
    final dados = await showDialog<({String forma, DateTime data, double valor, String? observacao})>(
        context: context, builder: (_) => _BaixaDialogo(fatura: fatura));
    if (dados == null) return;
    await _executar(
        () => _api.baixarFatura(fatura.id, forma: dados.forma, pagaEm: dados.data, valorPago: dados.valor, observacao: dados.observacao),
        'Pagamento de ${fatura.nomeEmpresa} registrado.');
  }

  Future<void> _cancelar(Fatura fatura) async {
    final motivo = TextEditingController();
    final confirmou = await showDialog<bool>(
      context: context,
      builder: (contexto) => AlertDialog(
        title: const Text('Cancelar fatura?'),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('${fatura.nomeEmpresa} · ${fatura.descricao} · ${Formatadores.moeda(fatura.valor)}'),
          const SizedBox(height: 12),
          TextField(controller: motivo, autofocus: true, decoration: const InputDecoration(labelText: 'Motivo')),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.of(contexto).pop(false), child: const Text('Voltar')),
          TextButton(onPressed: () => Navigator.of(contexto).pop(true), child: const Text('Cancelar fatura')),
        ],
      ),
    );
    final texto = motivo.text.trim();
    motivo.dispose();
    if (confirmou != true) return;
    await _executar(() => _api.cancelarFatura(fatura.id, texto), 'Fatura cancelada.');
  }

  Future<void> _lancar() async {
    final lancou = await showDialog<bool>(context: context, builder: (_) => _AvulsaDialogo(empresas: widget.empresas));
    if (lancou == true) {
      await _carregar();
      if (mounted) mostrarAviso(context, 'Cobrança lançada e enviada por e-mail ao administrador.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final faturas = _faturas;
    final emAberto = faturas?.where((f) => f.emAberto).fold<double>(0, (t, f) => t + f.valor) ?? 0;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Faturas do PkLots'),
        actions: [
          IconButton(tooltip: 'Chave PIX do PkLots', icon: const Icon(Icons.pix), onPressed: () => _configurarPix()),
          IconButton(tooltip: 'Emitir mensalidades agora', icon: const Icon(Icons.playlist_add_check), onPressed: _emitir),
          IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: _carregar),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: widget.empresas.isEmpty ? null : _lancar,
        backgroundColor: CoresPkLots.azulMarinho,
        foregroundColor: Colors.white,
        icon: const Icon(Icons.add),
        label: const Text('Cobrança avulsa'),
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 900),
          child: RefreshIndicator(
            onRefresh: _carregar,
            child: ListView(padding: const EdgeInsets.fromLTRB(16, 16, 16, 88), children: [
              Wrap(spacing: 8, runSpacing: 8, children: [
                for (final (valor, rotulo) in _filtros)
                  ChoiceChip(
                    label: Text(rotulo),
                    selected: _filtro == valor,
                    onSelected: (_) {
                      setState(() {
                        _filtro = valor;
                        _faturas = null;
                      });
                      _carregar();
                    },
                  ),
              ]),
              const SizedBox(height: 12),
              if (faturas != null && emAberto > 0)
                Text('Em aberto nesta lista: ${Formatadores.moeda(emAberto)}',
                    style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.azulMarinho)),
              const SizedBox(height: 8),
              if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
              if (faturas == null && _erro == null) const Center(child: CircularProgressIndicator()),
              if (faturas != null && faturas.isEmpty)
                const Padding(
                  padding: EdgeInsets.all(24),
                  child: Center(child: Text('Nenhuma fatura neste filtro.', style: TextStyle(color: CoresPkLots.textoSuave))),
                ),
              for (final fatura in faturas ?? <Fatura>[])
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(14),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Row(children: [
                        Expanded(
                          child: Text(fatura.nomeEmpresa,
                              style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                        ),
                        Text(Formatadores.moeda(fatura.valor),
                            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16, color: CoresPkLots.azulMarinho)),
                      ]),
                      const SizedBox(height: 2),
                      Text(fatura.descricao, style: const TextStyle(color: CoresPkLots.textoSecundario)),
                      if (fatura.patiosCobrados != null)
                        Text('${fatura.patiosCobrados} pátio(s) × ${Formatadores.moeda(fatura.precoPorPatio ?? 0)}',
                            style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                      const SizedBox(height: 6),
                      Wrap(spacing: 12, runSpacing: 4, crossAxisAlignment: WrapCrossAlignment.center, children: [
                        SituacaoFatura(fatura: fatura),
                        Text('Vence ${Formatadores.data(fatura.vencimento)}', style: const TextStyle(fontSize: 13)),
                        if (fatura.pagaEm != null)
                          Text(
                              'Paga em ${Formatadores.data(fatura.pagaEm!.toLocal())} · '
                              '${rotulosFormaPagamento[fatura.formaPagamento] ?? fatura.formaPagamento} · ${Formatadores.moeda(fatura.valorPago ?? 0)}',
                              style: const TextStyle(fontSize: 13)),
                        if (fatura.motivoCancelamento != null)
                          Text('Motivo: ${fatura.motivoCancelamento}', style: const TextStyle(fontSize: 13)),
                      ]),
                      if (fatura.emAberto)
                        Align(
                          alignment: Alignment.centerRight,
                          child: Wrap(spacing: 4, children: [
                            if (fatura.pixCopiaECola != null)
                              TextButton.icon(
                                  onPressed: () => mostrarPixFatura(context, fatura),
                                  icon: const Icon(Icons.qr_code_2),
                                  label: const Text('PIX')),
                            TextButton.icon(
                                onPressed: () => _cancelar(fatura), icon: const Icon(Icons.block), label: const Text('Cancelar')),
                            FilledButton.icon(
                                onPressed: () => _baixar(fatura), icon: const Icon(Icons.check), label: const Text('Dar baixa')),
                          ]),
                        ),
                    ]),
                  ),
                ),
            ]),
          ),
        ),
      ),
    );
  }

  Future<void> _configurarPix() async {
    RecebimentoPlataforma atual;
    try {
      atual = await _api.recebimentoPlataforma();
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
      return;
    }
    if (!mounted) return;
    final salvou = await showDialog<bool>(context: context, builder: (_) => _PixPlataformaDialogo(atual: atual));
    if (salvou == true) {
      await _carregar();
      if (mounted) mostrarAviso(context, 'Chave PIX do PkLots salva: vale para todas as faturas em aberto.');
    }
  }
}

/// Registra o recebimento (baixa manual)
class _BaixaDialogo extends StatefulWidget {
  const _BaixaDialogo({required this.fatura});

  final Fatura fatura;

  @override
  State<_BaixaDialogo> createState() => _BaixaDialogoState();
}

class _BaixaDialogoState extends State<_BaixaDialogo> {
  final _formulario = GlobalKey<FormState>();
  late final _valor = TextEditingController(text: widget.fatura.valor.toStringAsFixed(2).replaceAll('.', ','));
  final _observacao = TextEditingController();
  String _forma = 'pix';
  DateTime _data = DateTime.now();

  @override
  void dispose() {
    _valor.dispose();
    _observacao.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Dar baixa'),
        content: SizedBox(
          width: 400,
          child: Form(
            key: _formulario,
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text('${widget.fatura.nomeEmpresa} · ${widget.fatura.descricao}'),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _forma,
                decoration: const InputDecoration(labelText: 'Forma de pagamento'),
                items: [for (final e in rotulosFormaPagamento.entries) DropdownMenuItem(value: e.key, child: Text(e.value))],
                onChanged: (v) => setState(() => _forma = v ?? _forma),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _valor,
                decoration: const InputDecoration(labelText: 'Valor recebido', prefixText: 'R\$ '),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                validator: (v) => (lerReais(v ?? '') ?? 0) > 0 ? null : 'Valor inválido',
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.event_available_outlined),
                title: Text('Recebido em ${Formatadores.data(_data)}'),
                trailing: const Icon(Icons.edit_calendar_outlined),
                onTap: () async {
                  final hoje = DateTime.now();
                  final data = await showDatePicker(
                      context: context, firstDate: DateTime(hoje.year - 1), lastDate: hoje, initialDate: _data);
                  if (data != null) setState(() => _data = data);
                },
              ),
              TextFormField(controller: _observacao, decoration: const InputDecoration(labelText: 'Observação (opcional)')),
            ]),
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
          FilledButton(
            onPressed: () {
              if (!_formulario.currentState!.validate()) return;
              Navigator.of(context).pop((
                forma: _forma,
                data: _data,
                valor: lerReais(_valor.text)!,
                observacao: _observacao.text.trim().isEmpty ? null : _observacao.text.trim(),
              ));
            },
            child: const Text('Confirmar'),
          ),
        ],
      );
}

/// Cobrança avulsa (ex.: taxa de implantação)
class _AvulsaDialogo extends StatefulWidget {
  const _AvulsaDialogo({required this.empresas});

  final List<EmpresaPlataforma> empresas;

  @override
  State<_AvulsaDialogo> createState() => _AvulsaDialogoState();
}

class _AvulsaDialogoState extends State<_AvulsaDialogo> {
  final _formulario = GlobalKey<FormState>();
  final _descricao = TextEditingController(text: 'Taxa de implantação');
  final _valor = TextEditingController();
  String? _empresaId;
  DateTime _vencimento = DateTime.now().add(const Duration(days: 7));
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _descricao.dispose();
    _valor.dispose();
    super.dispose();
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<ClienteApi>().lancarFatura(_empresaId!, _descricao.text.trim(), lerReais(_valor.text)!, _vencimento);
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Cobrança avulsa'),
        content: SizedBox(
          width: 420,
          child: Form(
            key: _formulario,
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              DropdownButtonFormField<String>(
                initialValue: _empresaId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Empresa'),
                items: [
                  for (final e in widget.empresas.where((e) => e.ativa))
                    DropdownMenuItem(value: e.id, child: Text(e.nome, overflow: TextOverflow.ellipsis)),
                ],
                onChanged: (id) => setState(() => _empresaId = id),
                validator: (v) => v == null ? 'Escolha a empresa' : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _descricao,
                maxLength: 120,
                decoration: const InputDecoration(labelText: 'Descrição'),
                validator: (v) => v == null || v.trim().isEmpty ? 'Informe a descrição' : null,
              ),
              TextFormField(
                controller: _valor,
                decoration: const InputDecoration(labelText: 'Valor', prefixText: 'R\$ '),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                validator: (v) => (lerReais(v ?? '') ?? 0) > 0 ? null : 'Valor inválido',
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.event_outlined),
                title: Text('Vencimento ${Formatadores.data(_vencimento)}'),
                trailing: const Icon(Icons.edit_calendar_outlined),
                onTap: () async {
                  final hoje = DateTime.now();
                  final data = await showDatePicker(
                      context: context, firstDate: hoje, lastDate: DateTime(hoje.year + 1, hoje.month, hoje.day), initialDate: _vencimento);
                  if (data != null) setState(() => _vencimento = data);
                },
              ),
              if (_erro != null) Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
            ]),
          ),
        ),
        actions: [
          TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
          FilledButton(onPressed: _enviando ? null : _salvar, child: const Text('Lançar')),
        ],
      );
}

/// Chave PIX do PkLots: vai em todas as faturas em aberto
class _PixPlataformaDialogo extends StatefulWidget {
  const _PixPlataformaDialogo({required this.atual});

  final RecebimentoPlataforma atual;

  @override
  State<_PixPlataformaDialogo> createState() => _PixPlataformaDialogoState();
}

class _PixPlataformaDialogoState extends State<_PixPlataformaDialogo> {
  late final _chave = TextEditingController(text: widget.atual.chavePix);
  late final _nome = TextEditingController(text: widget.atual.nomeRecebedor);
  late final _cidade = TextEditingController(text: widget.atual.cidadeRecebedor);
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    for (final c in [_chave, _nome, _cidade]) {
      c.dispose();
    }
    super.dispose();
  }

  String? _texto(TextEditingController c) => c.text.trim().isEmpty ? null : c.text.trim();

  Future<void> _salvar() async {
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await context.read<ClienteApi>().salvarRecebimentoPlataforma(_texto(_chave), _texto(_nome), _texto(_cidade));
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Chave PIX do PkLots'),
        content: SizedBox(
          width: 420,
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const Text('Usada no PIX copia e cola de todas as faturas em aberto. Deixe a chave em branco para emitir sem PIX.',
                style: TextStyle(color: CoresPkLots.textoSecundario)),
            const SizedBox(height: 12),
            TextField(controller: _chave, decoration: const InputDecoration(labelText: 'Chave PIX')),
            const SizedBox(height: 12),
            TextField(controller: _nome, maxLength: 25, decoration: const InputDecoration(labelText: 'Nome do recebedor')),
            TextField(controller: _cidade, maxLength: 15, decoration: const InputDecoration(labelText: 'Cidade do recebedor')),
            if (_erro != null) Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
          ]),
        ),
        actions: [
          TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
          FilledButton(onPressed: _enviando ? null : _salvar, child: const Text('Salvar')),
        ],
      );
}
