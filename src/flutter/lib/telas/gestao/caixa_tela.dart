import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../estado/sessao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/caixa_dialogos.dart';
import '../../widgets/estados_tela.dart';

/// Caixa do usuário (abrir, sangria/suprimento, fechar) e caixas do dia no pátio
class CaixaTela extends StatefulWidget {
  const CaixaTela({super.key});

  @override
  State<CaixaTela> createState() => _CaixaTelaState();
}

class _CaixaTelaState extends State<CaixaTela> {
  List<Caixa>? _doDia;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  Future<void> _carregar() async {
    final estado = context.read<GestaoProvider>();
    await estado.carregarCaixa();
    try {
      final lista = await estado.caixasDoDia();
      if (mounted) setState(() => _doDia = lista);
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  Future<void> _fechar(Caixa caixa) async {
    final fechado = await showDialog<Caixa>(context: context, builder: (_) => _FecharCaixa(caixa: caixa));
    if (fechado == null || !mounted) return;
    final diferenca = fechado.diferenca ?? 0;
    mostrarAviso(
      context,
      diferenca == 0
          ? 'Caixa fechado sem diferença.'
          : 'Caixa fechado com diferença de ${Formatadores.moeda(diferenca)}.',
      erro: diferenca != 0,
    );
    await _carregar();
  }

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<GestaoProvider>();
    final ehAdmin = context.watch<SessaoProvider>().ehAdmin;
    final caixa = estado.caixa;

    return Scaffold(
      appBar: AppBar(
        title: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Caixa'),
          if (estado.patioAtual != null)
            Text(estado.patioAtual!.nome, style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
        ]),
        actions: [IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: _carregar)],
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 720),
          child: RefreshIndicator(
            onRefresh: _carregar,
            child: ListView(padding: const EdgeInsets.all(16), children: [
              if (caixa == null)
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                      const TituloSecao('Seu caixa está fechado', icone: Icons.lock_outline),
                      const Text('Abra o caixa com o troco inicial para receber pagamentos de saídas e mensalidades.',
                          style: TextStyle(color: CoresPkLots.textoSecundario)),
                      const SizedBox(height: 16),
                      FilledButton.icon(
                        onPressed: () async {
                          if (await mostrarAbrirCaixa(context) == true) await _carregar();
                        },
                        icon: const Icon(Icons.lock_open),
                        label: const Text('Abrir caixa'),
                      ),
                    ]),
                  ),
                )
              else
                _ResumoCaixa(
                  caixa: caixa,
                  acoes: Wrap(spacing: 8, runSpacing: 8, children: [
                    OutlinedButton.icon(
                      onPressed: () async {
                        if (await mostrarMovimentoCaixa(context, sangria: false) == true) await _carregar();
                      },
                      icon: const Icon(Icons.add_circle_outline),
                      label: const Text('Suprimento'),
                    ),
                    OutlinedButton.icon(
                      onPressed: () async {
                        if (await mostrarMovimentoCaixa(context, sangria: true) == true) await _carregar();
                      },
                      icon: const Icon(Icons.remove_circle_outline),
                      label: const Text('Sangria'),
                    ),
                    FilledButton.icon(
                      onPressed: () => _fechar(caixa),
                      icon: const Icon(Icons.lock_outline),
                      label: const Text('Fechar caixa'),
                    ),
                  ]),
                ),
              const SizedBox(height: 20),
              TituloSecao(ehAdmin ? 'Caixas de hoje no pátio' : 'Seus caixas de hoje', icone: Icons.today),
              if (_doDia == null) const Center(child: CircularProgressIndicator()),
              if (_doDia != null && _doDia!.isEmpty)
                const Text('Nenhum caixa hoje.', style: TextStyle(color: CoresPkLots.textoSecundario)),
              for (final c in _doDia ?? <Caixa>[]) ...[
                Card(
                  child: ListTile(
                    leading: Icon(c.aberto ? Icons.lock_open : Icons.lock_outline,
                        color: c.aberto ? CoresPkLots.disponivel : CoresPkLots.textoSecundario),
                    title: Text('${c.nomeOperador} · ${Formatadores.hora(c.abertoEm)}'
                        '${c.fechadoEm != null ? ' – ${Formatadores.hora(c.fechadoEm!)}' : ' (aberto)'}'),
                    subtitle: Text([
                      'Recebido ${Formatadores.moeda(c.totalRecebido)}',
                      '${c.saidas} saída(s)',
                      if (c.diferenca != null) 'Diferença ${Formatadores.moeda(c.diferenca!)}',
                    ].join(' · ')),
                    trailing: c.diferenca != null && c.diferenca != 0
                        ? const Tooltip(message: 'Fechado com diferença', child: Icon(Icons.warning_amber_rounded, color: CoresPkLots.manutencao))
                        : null,
                  ),
                ),
                const SizedBox(height: 8),
              ],
            ]),
          ),
        ),
      ),
    );
  }
}

class _ResumoCaixa extends StatelessWidget {
  const _ResumoCaixa({required this.caixa, required this.acoes});

  final Caixa caixa;
  final Widget acoes;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            TituloSecao('Caixa aberto às ${Formatadores.hora(caixa.abertoEm)}', icone: Icons.lock_open),
            const Text('Dinheiro esperado na gaveta', style: TextStyle(color: CoresPkLots.textoSecundario)),
            Text(Formatadores.moeda(caixa.dinheiroEsperado),
                style: const TextStyle(fontSize: 32, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho)),
            const Divider(height: 28),
            _Linha('Troco inicial', Formatadores.moeda(caixa.trocoInicial)),
            for (final r in caixa.recebimentos)
              _Linha('${rotulosFormaPagamento[r.forma] ?? r.forma} (${r.quantidade})', Formatadores.moeda(r.total)),
            if (caixa.suprimentos > 0) _Linha('Suprimentos', '+ ${Formatadores.moeda(caixa.suprimentos)}'),
            if (caixa.sangrias > 0) _Linha('Sangrias', '− ${Formatadores.moeda(caixa.sangrias)}'),
            _Linha('Total recebido', Formatadores.moeda(caixa.totalRecebido), destaque: true),
            const SizedBox(height: 16),
            acoes,
          ]),
        ),
      );
}

class _Linha extends StatelessWidget {
  const _Linha(this.rotulo, this.valor, {this.destaque = false});

  final String rotulo;
  final String valor;
  final bool destaque;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(children: [
          Expanded(child: Text(rotulo, style: const TextStyle(color: CoresPkLots.textoSecundario))),
          Text(valor,
              style: TextStyle(
                  fontWeight: destaque ? FontWeight.w800 : FontWeight.w600, color: CoresPkLots.azulMarinho)),
        ]),
      );
}

class _FecharCaixa extends StatefulWidget {
  const _FecharCaixa({required this.caixa});

  final Caixa caixa;

  @override
  State<_FecharCaixa> createState() => _FecharCaixaState();
}

class _FecharCaixaState extends State<_FecharCaixa> {
  final _contado = TextEditingController();
  final _observacao = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void initState() {
    super.initState();
    _contado.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _contado.dispose();
    _observacao.dispose();
    super.dispose();
  }

  Future<void> _fechar() async {
    final contado = lerReais(_contado.text);
    if (contado == null || contado < 0) {
      setState(() => _erro = 'Informe o dinheiro contado na gaveta.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      final fechado = await context
          .read<GestaoProvider>()
          .fecharCaixa(contado, _observacao.text.trim().isEmpty ? null : _observacao.text.trim());
      if (mounted) Navigator.of(context).pop(fechado);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final contado = lerReais(_contado.text);
    final diferenca = contado == null ? null : contado - widget.caixa.dinheiroEsperado;
    return AlertDialog(
      title: const Text('Fechar caixa'),
      content: SizedBox(
        width: 380,
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          const Text('Conte o dinheiro da gaveta e informe o total.', style: TextStyle(color: CoresPkLots.textoSecundario)),
          const SizedBox(height: 12),
          TextField(
            controller: _contado,
            autofocus: true,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            inputFormatters: [filtroReais],
            decoration: const InputDecoration(labelText: 'Dinheiro contado', prefixText: 'R\$ '),
          ),
          const SizedBox(height: 10),
          if (diferenca != null)
            Text(
              diferenca == 0
                  ? 'Confere com o esperado.'
                  : diferenca > 0
                      ? 'Sobram ${Formatadores.moeda(diferenca)}'
                      : 'Faltam ${Formatadores.moeda(-diferenca)}',
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: diferenca == 0 ? CoresPkLots.disponivel : CoresPkLots.erro,
              ),
            ),
          const SizedBox(height: 10),
          TextField(controller: _observacao, decoration: const InputDecoration(labelText: 'Observação (opcional)')),
          if (_erro != null) ...[
            const SizedBox(height: 10),
            Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
          ],
        ]),
      ),
      actions: [
        TextButton(onPressed: _enviando ? null : () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        SizedBox(
          width: 140,
          child: FilledButton(
            onPressed: _enviando ? null : _fechar,
            child: _enviando
                ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Fechar caixa'),
          ),
        ),
      ],
    );
  }
}
