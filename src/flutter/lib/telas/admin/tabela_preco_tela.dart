import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/caixa_dialogos.dart';
import '../../widgets/estados_tela.dart';

/// Tabela de preço do pátio: tolerância, por hora ou primeiro período + frações, diária máxima, pernoite e fim de semana
class TabelaPrecoTela extends StatefulWidget {
  const TabelaPrecoTela({super.key, required this.nomePatio});

  final String nomePatio;

  @override
  State<TabelaPrecoTela> createState() => _TabelaPrecoTelaState();
}

class _TabelaPrecoTelaState extends State<TabelaPrecoTela> {
  final _formulario = GlobalKey<FormState>();
  _ControleRegras? _semana;
  _ControleRegras? _fimDeSemana;
  final _tolerancia = TextEditingController();
  String? _erro;
  bool _salvando = false;

  // Simulação
  int _horaEntrada = 9;
  bool _simularFimDeSemana = false;
  List<(String, SimulacaoTarifa?)>? _simulacoes;
  String? _erroSimulacao;
  bool _simulando = false;

  static const _atalhosTolerancia = [0, 10, 15, 20, 30];

  static const _duracoes = [('15 min', 15), ('30 min', 30), ('1h', 60), ('1h 30min', 90), ('3h', 180), ('8h', 480), ('14h', 840), ('26h', 1560)];

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  @override
  void dispose() {
    _semana?.dispose();
    _fimDeSemana?.dispose();
    _tolerancia.dispose();
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final tabela = await _api.tabelaPreco();
      if (!mounted) return;
      setState(() {
        _semana = _ControleRegras(tabela.semana);
        _fimDeSemana = tabela.fimDeSemana == null ? null : _ControleRegras(tabela.fimDeSemana!);
        _tolerancia.text = '${tabela.minutosTolerancia}';
        _erro = null;
      });
      await _simular();
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  /// Próximo dia útil (ou sábado) no horário escolhido
  DateTime _entradaSimulada() {
    final hoje = DateTime.now();
    var dia = DateTime(hoje.year, hoje.month, hoje.day, _horaEntrada);
    bool fimDeSemana(DateTime d) => d.weekday == DateTime.saturday || d.weekday == DateTime.sunday;
    while (fimDeSemana(dia) != _simularFimDeSemana || (_simularFimDeSemana && dia.weekday != DateTime.saturday)) {
      dia = dia.add(const Duration(days: 1));
    }
    return dia;
  }

  Future<void> _simular() async {
    final semana = _semana?.regras();
    final fimDeSemana = _fimDeSemana?.regras();
    final tolerancia = _minutosTolerancia;
    if (semana == null || (_fimDeSemana != null && fimDeSemana == null) || tolerancia == null) {
      setState(() => _erroSimulacao = 'Preencha os valores da tabela para simular.');
      return;
    }
    setState(() {
      _simulando = true;
      _erroSimulacao = null;
    });
    try {
      final entrada = _entradaSimulada();
      final resultados = await Future.wait([
        for (final (_, minutos) in _duracoes)
          _api.simularTarifa(semana, fimDeSemana, entrada, entrada.add(Duration(minutes: minutos)), minutosTolerancia: tolerancia),
      ]);
      if (mounted) setState(() => _simulacoes = [for (final (i, d) in _duracoes.indexed) (d.$1, resultados[i])]);
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erroSimulacao = e.mensagem);
    } finally {
      if (mounted) setState(() => _simulando = false);
    }
  }

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() => _salvando = true);
    try {
      await _api.salvarTabelaPreco(_semana!.regras()!, _fimDeSemana?.regras(), minutosTolerancia: _minutosTolerancia);
      if (mounted) {
        mostrarAviso(context, 'Tabela de preço salva. Vale para as próximas saídas.');
        Navigator.of(context).pop(true);
      }
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  int? get _minutosTolerancia {
    final minutos = int.tryParse(_tolerancia.text);
    return minutos == null || minutos > 240 ? null : minutos;
  }

  void _alternarFimDeSemana(bool ativo) => setState(() {
        _fimDeSemana?.dispose();
        _fimDeSemana = ativo ? _ControleRegras(_semana?.regras() ?? const RegrasPreco()) : null;
      });

  @override
  Widget build(BuildContext context) {
    final semana = _semana;

    return Scaffold(
      appBar: AppBar(
        title: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Tabela de preço'),
          Text(widget.nomePatio, style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
        ]),
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 760),
          child: _erro != null
              ? Padding(padding: const EdgeInsets.all(16), child: MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar))
              : semana == null
                  ? const Center(child: CircularProgressIndicator())
                  : Form(
                      key: _formulario,
                      child: ListView(padding: const EdgeInsets.all(16), children: [
                        _toleranciaGratuita(),
                        const SizedBox(height: 12),
                        _EditorRegras(
                          titulo: _fimDeSemana == null ? 'Todos os dias' : 'Segunda a sexta',
                          controle: semana,
                          aoMudar: () => setState(() {}),
                        ),
                        const SizedBox(height: 12),
                        Card(
                          child: SwitchListTile(
                            value: _fimDeSemana != null,
                            onChanged: _alternarFimDeSemana,
                            secondary: const Icon(Icons.weekend_outlined),
                            title: const Text('Preço diferente no fim de semana'),
                            subtitle: const Text('Vale para quem entra no sábado ou no domingo'),
                          ),
                        ),
                        if (_fimDeSemana case final fds?) ...[
                          const SizedBox(height: 12),
                          _EditorRegras(titulo: 'Sábado e domingo', controle: fds, aoMudar: () => setState(() {})),
                        ],
                        const SizedBox(height: 12),
                        _simulacao(),
                        const SizedBox(height: 20),
                        FilledButton.icon(
                          onPressed: _salvando ? null : _salvar,
                          icon: _salvando
                              ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                              : const Icon(Icons.save_outlined),
                          label: const Text('Salvar tabela'),
                        ),
                        const SizedBox(height: 8),
                        const Text(
                          'Saídas já registradas não mudam de valor.',
                          textAlign: TextAlign.center,
                          style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave),
                        ),
                      ]),
                    ),
        ),
      ),
    );
  }

  Widget _toleranciaGratuita() {
    final tolerancia = int.tryParse(_tolerancia.text);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          const TituloSecao('Tolerância gratuita', icone: Icons.timer_outlined),
          const Text('Quem sair dentro da tolerância não paga. Passou dela, é cobrado o tempo todo pela tabela abaixo.',
              style: TextStyle(color: CoresPkLots.textoSecundario)),
          const SizedBox(height: 14),
          TextFormField(
            controller: _tolerancia,
            decoration: const InputDecoration(labelText: 'Tolerância', suffixText: 'minutos'),
            keyboardType: TextInputType.number,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            onChanged: (_) => setState(() {}),
            validator: (v) {
              final minutos = int.tryParse(v ?? '');
              if (minutos == null) return 'Informe os minutos (0 desativa)';
              if (minutos > 240) return 'Máximo de 240 minutos';
              return null;
            },
          ),
          const SizedBox(height: 10),
          Wrap(spacing: 8, runSpacing: 8, children: [
            for (final minutos in _atalhosTolerancia)
              ChoiceChip(
                label: Text(minutos == 0 ? 'Sem tolerância' : '$minutos min'),
                selected: tolerancia == minutos,
                onSelected: (_) {
                  setState(() => _tolerancia.text = '$minutos');
                  _simular();
                },
              ),
          ]),
        ]),
      ),
    );
  }

  Widget _simulacao() => Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            TituloSecao('Simulação', icone: Icons.calculate_outlined,
                acao: TextButton.icon(
                  onPressed: _simulando ? null : _simular,
                  icon: const Icon(Icons.refresh, size: 18),
                  label: const Text('Simular'),
                )),
            Wrap(spacing: 12, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.center, children: [
              SegmentedButton<bool>(
                segments: const [
                  ButtonSegment(value: false, label: Text('Dia de semana')),
                  ButtonSegment(value: true, label: Text('Sábado')),
                ],
                selected: {_simularFimDeSemana},
                onSelectionChanged: (s) {
                  setState(() => _simularFimDeSemana = s.first);
                  _simular();
                },
              ),
              SizedBox(
                width: 170,
                child: DropdownButtonFormField<int>(
                  initialValue: _horaEntrada,
                  decoration: const InputDecoration(labelText: 'Entrada às'),
                  items: [for (var h = 0; h < 24; h++) DropdownMenuItem(value: h, child: Text('${h.toString().padLeft(2, '0')}:00'))],
                  onChanged: (h) {
                    setState(() => _horaEntrada = h ?? _horaEntrada);
                    _simular();
                  },
                ),
              ),
            ]),
            const SizedBox(height: 12),
            if (_erroSimulacao != null) Text(_erroSimulacao!, style: const TextStyle(color: CoresPkLots.erro)),
            if (_simulando && _simulacoes == null) const LinearProgressIndicator(),
            for (final (rotulo, s) in _simulacoes ?? <(String, SimulacaoTarifa?)>[])
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 5),
                child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  SizedBox(width: 110, child: Text('Ficou $rotulo', style: const TextStyle(color: CoresPkLots.textoSecundario))),
                  Expanded(
                    child: Text(s?.detalhamento ?? '', style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    s == null ? '-' : (s.valor == 0 ? 'Grátis' : Formatadores.moeda(s.valor)),
                    style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho),
                  ),
                ]),
              ),
          ]),
        ),
      );
}

/// Valores editáveis de um conjunto de regras (semana ou fim de semana)
class _ControleRegras {
  _ControleRegras(RegrasPreco r)
      : modo = r.modo,
        valorHora = TextEditingController(text: _texto(r.valorHora)),
        primeiroPeriodo = TextEditingController(text: '${r.primeiroPeriodoMinutos}'),
        valorPrimeiro = TextEditingController(text: _texto(r.valorPrimeiroPeriodo)),
        fracao = TextEditingController(text: '${r.fracaoMinutos}'),
        valorFracao = TextEditingController(text: _texto(r.valorFracao)),
        temDiaria = r.diariaMaxima != null,
        diaria = TextEditingController(text: r.diariaMaxima == null ? '' : _texto(r.diariaMaxima!)),
        temPernoite = r.valorPernoite != null,
        pernoite = TextEditingController(text: r.valorPernoite == null ? '' : _texto(r.valorPernoite!)),
        inicioPernoite = r.inicioPernoite,
        fimPernoite = r.fimPernoite;

  String modo;
  final TextEditingController valorHora, primeiroPeriodo, valorPrimeiro, fracao, valorFracao, diaria, pernoite;
  bool temDiaria, temPernoite;
  int inicioPernoite, fimPernoite;

  static String _texto(double v) => v.toStringAsFixed(2).replaceAll('.', ',');

  /// Regras com os valores digitados (nulo se algum número obrigatório estiver inválido)
  RegrasPreco? regras() {
    final hora = lerReais(valorHora.text);
    final primeiro = int.tryParse(primeiroPeriodo.text);
    final vPrimeiro = lerReais(valorPrimeiro.text);
    final f = int.tryParse(fracao.text);
    final vFracao = lerReais(valorFracao.text);
    if (modo == RegrasPreco.hora && hora == null) return null;
    if (modo == RegrasPreco.fracao && (primeiro == null || vPrimeiro == null || f == null || vFracao == null)) return null;
    if (temDiaria && lerReais(diaria.text) == null) return null;
    if (temPernoite && lerReais(pernoite.text) == null) return null;
    return RegrasPreco(
      modo: modo,
      valorHora: hora ?? 0,
      primeiroPeriodoMinutos: primeiro ?? 60,
      valorPrimeiroPeriodo: vPrimeiro ?? 0,
      fracaoMinutos: f ?? 60,
      valorFracao: vFracao ?? 0,
      diariaMaxima: temDiaria ? lerReais(diaria.text) : null,
      valorPernoite: temPernoite ? lerReais(pernoite.text) : null,
      inicioPernoite: inicioPernoite,
      fimPernoite: fimPernoite,
    );
  }

  void dispose() {
    for (final c in [valorHora, primeiroPeriodo, valorPrimeiro, fracao, valorFracao, diaria, pernoite]) {
      c.dispose();
    }
  }
}

class _EditorRegras extends StatelessWidget {
  const _EditorRegras({required this.titulo, required this.controle, required this.aoMudar});

  final String titulo;
  final _ControleRegras controle;
  final VoidCallback aoMudar;

  static final _numeros = [FilteringTextInputFormatter.allow(RegExp(r'[\d.,]'))];

  static String? _valor(String? v, {bool permitirZero = false}) {
    final n = lerReais(v ?? '');
    if (n == null) return 'Informe o valor';
    if (n < 0 || (!permitirZero && n == 0) || n > 1000) return 'Até R\$ 1.000,00';
    return null;
  }

  static String? _minutos(String? v) {
    final n = int.tryParse(v ?? '');
    return n == null || n < 1 || n > 1440 ? '1 a 1440' : null;
  }

  Widget _campoValor(TextEditingController c, String rotulo, {bool permitirZero = false}) => TextFormField(
        controller: c,
        decoration: InputDecoration(labelText: rotulo, prefixText: 'R\$ '),
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        inputFormatters: _numeros,
        validator: (v) => _valor(v, permitirZero: permitirZero),
      );

  Widget _campoMinutos(TextEditingController c, String rotulo) => TextFormField(
        controller: c,
        decoration: InputDecoration(labelText: rotulo, suffixText: 'min'),
        keyboardType: TextInputType.number,
        inputFormatters: [FilteringTextInputFormatter.digitsOnly],
        validator: _minutos,
      );

  Widget _hora(String rotulo, int valor, ValueChanged<int> aoEscolher) => DropdownButtonFormField<int>(
        initialValue: valor,
        decoration: InputDecoration(labelText: rotulo),
        items: [for (var h = 0; h < 24; h++) DropdownMenuItem(value: h, child: Text('${h.toString().padLeft(2, '0')}:00'))],
        onChanged: (h) {
          if (h != null) aoEscolher(h);
          aoMudar();
        },
      );

  @override
  Widget build(BuildContext context) {
    final c = controle;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          TituloSecao(titulo, icone: Icons.sell_outlined),
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: RegrasPreco.hora, label: Text('Por hora'), icon: Icon(Icons.schedule)),
              ButtonSegment(value: RegrasPreco.fracao, label: Text('1º período + frações'), icon: Icon(Icons.splitscreen_outlined)),
            ],
            selected: {c.modo},
            onSelectionChanged: (s) {
              c.modo = s.first;
              aoMudar();
            },
          ),
          const SizedBox(height: 14),
          if (c.modo == RegrasPreco.hora) ...[
            _campoValor(c.valorHora, 'Valor por hora'),
            const SizedBox(height: 6),
            const Text('Cobrado proporcionalmente ao tempo, arredondado para cima ao minuto.',
                style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
          ] else ...[
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Expanded(child: _campoMinutos(c.primeiroPeriodo, 'Primeiro período')),
              const SizedBox(width: 12),
              Expanded(child: _campoValor(c.valorPrimeiro, 'Valor', permitirZero: true)),
            ]),
            const SizedBox(height: 12),
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Expanded(child: _campoMinutos(c.fracao, 'Cada fração adicional')),
              const SizedBox(width: 12),
              Expanded(child: _campoValor(c.valorFracao, 'Valor da fração', permitirZero: true)),
            ]),
            const SizedBox(height: 6),
            const Text('Ex.: 1ª hora (60 min) R\$ 10,00 e cada 30 min ou fração R\$ 3,00. Fração iniciada é cobrada inteira.',
                style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
          ],
          const Divider(height: 28),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            value: c.temDiaria,
            onChanged: (v) {
              c.temDiaria = v;
              aoMudar();
            },
            title: const Text('Diária máxima'),
            subtitle: const Text('Ninguém paga mais que isso a cada 24 horas'),
          ),
          if (c.temDiaria) _campoValor(c.diaria, 'Diária máxima'),
          const SizedBox(height: 8),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            value: c.temPernoite,
            onChanged: (v) {
              c.temPernoite = v;
              aoMudar();
            },
            title: const Text('Pernoite'),
            subtitle: const Text('Dentro do horário noturno, cada noite custa no máximo o valor do pernoite'),
          ),
          if (c.temPernoite)
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Expanded(flex: 2, child: _campoValor(c.pernoite, 'Valor do pernoite')),
              const SizedBox(width: 12),
              Expanded(child: _hora('Das', c.inicioPernoite, (h) => c.inicioPernoite = h)),
              const SizedBox(width: 12),
              Expanded(child: _hora('Até', c.fimPernoite, (h) => c.fimPernoite = h)),
            ]),
        ]),
      ),
    );
  }
}
