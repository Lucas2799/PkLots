import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../widgets/estados_tela.dart';

/// Quantidade de vagas do pátio (fileiras de até 10 vagas: A1..A10, B1..)
class AbaVagas extends StatefulWidget {
  const AbaVagas({super.key, required this.configuracao});

  final ConfiguracaoPatio configuracao;

  @override
  State<AbaVagas> createState() => _AbaVagasState();
}

class _AbaVagasState extends State<AbaVagas> {
  late final _total = TextEditingController(text: '${widget.configuracao.totalVagas}');
  bool _salvando = false;

  ConfiguracaoPatio get _config => widget.configuracao;
  int? get _novoTotal => int.tryParse(_total.text);

  @override
  void initState() {
    super.initState();
    _total.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _total.dispose();
    super.dispose();
  }

  String _numero(int posicao) =>
      '${String.fromCharCode(65 + posicao ~/ _config.vagasPorFileira)}${posicao % _config.vagasPorFileira + 1}';

  void _ajustar(int delta) {
    final atual = _novoTotal ?? _config.totalVagas;
    _total.text = '${(atual + delta).clamp(1, _config.maximoVagas)}';
  }

  Future<void> _salvar() async {
    final novo = _novoTotal;
    if (novo == null || novo < 1 || novo > _config.maximoVagas) return;

    if (novo < _config.totalVagas) {
      final confirmou = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Reduzir vagas?'),
          content: Text('As vagas ${_numero(novo)} a ${_numero(_config.totalVagas - 1)} deixarão de ser usadas. '
              'O histórico delas é mantido. Se alguma estiver ocupada, a alteração será recusada.'),
          actions: [
            TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
            TextButton(onPressed: () => Navigator.of(context).pop(true), child: const Text('Reduzir')),
          ],
        ),
      );
      if (confirmou != true || !mounted) return;
    }

    setState(() => _salvando = true);
    try {
      await context.read<AdministracaoProvider>().alterarQuantidadeVagas(novo);
      if (mounted) mostrarAviso(context, 'Pátio agora tem $novo vagas.');
    } on ExcecaoApi catch (e) {
      if (mounted) mostrarAviso(context, e.mensagem, erro: true);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final novo = _novoTotal;
    final valido = novo != null && novo >= 1 && novo <= _config.maximoVagas;
    final diferenca = valido ? novo - _config.totalVagas : 0;
    final fileiras = valido ? (novo / _config.vagasPorFileira).ceil() : 0;

    return ListView(padding: const EdgeInsets.all(16), children: [
      Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const TituloSecao('Quantidade de vagas', icone: Icons.local_parking),
            Text('Hoje o pátio tem ${_config.totalVagas} vagas. '
                'As vagas são organizadas em fileiras de ${_config.vagasPorFileira} (A1 a A${_config.vagasPorFileira}, B1...).',
                style: const TextStyle(color: CoresPkLots.textoSecundario)),
            const SizedBox(height: 20),
            Row(children: [
              IconButton.outlined(
                tooltip: 'Menos uma vaga',
                onPressed: _salvando ? null : () => _ajustar(-1),
                icon: const Icon(Icons.remove),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: TextField(
                  controller: _total,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w800, color: CoresPkLots.azulMarinho),
                  keyboardType: TextInputType.number,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  decoration: InputDecoration(
                    errorText: valido ? null : 'Entre 1 e ${_config.maximoVagas}',
                    suffixText: 'vagas',
                  ),
                ),
              ),
              const SizedBox(width: 12),
              IconButton.outlined(
                tooltip: 'Mais uma vaga',
                onPressed: _salvando ? null : () => _ajustar(1),
                icon: const Icon(Icons.add),
              ),
            ]),
            const SizedBox(height: 16),
            if (valido)
              Text(
                'Fileiras A a ${String.fromCharCode(64 + fileiras)} · última vaga: ${_numero(novo - 1)}',
                textAlign: TextAlign.center,
                style: const TextStyle(color: CoresPkLots.textoSecundario),
              ),
            if (diferenca > 0) ...[
              const SizedBox(height: 12),
              _Aviso(Icons.add_circle_outline, CoresPkLots.disponivel,
                  'Serão criadas $diferenca vagas: ${_numero(_config.totalVagas)} a ${_numero(novo! - 1)}.'),
            ],
            if (diferenca < 0) ...[
              const SizedBox(height: 12),
              _Aviso(Icons.remove_circle_outline, CoresPkLots.manutencao,
                  'Serão desativadas ${-diferenca} vagas: ${_numero(novo!)} a ${_numero(_config.totalVagas - 1)}. '
                  'Vagas ocupadas impedem a redução.'),
            ],
          ]),
        ),
      ),
      const SizedBox(height: 20),
      FilledButton.icon(
        onPressed: _salvando || !valido || diferenca == 0 ? null : _salvar,
        icon: _salvando
            ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
            : const Icon(Icons.save_outlined),
        label: const Text('Salvar quantidade'),
      ),
    ]);
  }
}

class _Aviso extends StatelessWidget {
  const _Aviso(this.icone, this.cor, this.texto);

  final IconData icone;
  final Color cor;
  final String texto;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: cor.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(10)),
        child: Row(children: [
          Icon(icone, color: cor),
          const SizedBox(width: 10),
          Expanded(child: Text(texto)),
        ]),
      );
}
