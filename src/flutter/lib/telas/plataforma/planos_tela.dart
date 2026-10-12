import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../util/validadores.dart';
import '../../widgets/caixa_dialogos.dart';
import '../../widgets/estados_tela.dart';

/// Planos de aluguel do PkLots (superadmin): preço por pátio, limites e recursos liberados
class PlanosTela extends StatefulWidget {
  const PlanosTela({super.key});

  @override
  State<PlanosTela> createState() => _PlanosTelaState();
}

class _PlanosTelaState extends State<PlanosTela> {
  List<Plano>? _planos;
  List<RecursoPlano> _recursos = [];
  String? _erro;

  ClienteApi get _api => context.read<ClienteApi>();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  Future<void> _carregar() async {
    try {
      final resultados = await Future.wait([_api.planos(), _api.recursosDePlano()]);
      if (mounted) {
        setState(() {
          _planos = resultados[0] as List<Plano>;
          _recursos = resultados[1] as List<RecursoPlano>;
          _erro = null;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  Future<void> _editar([Plano? plano]) async {
    final salvo = await showDialog<bool>(context: context, builder: (_) => _EditorPlano(plano: plano, recursos: _recursos));
    if (salvo == true) await _carregar();
  }

  static String _limite(int? valor, String unidade) => valor == null ? '$unidade sem limite' : 'até $valor $unidade';

  @override
  Widget build(BuildContext context) {
    final planos = _planos;
    final nomes = {for (final r in _recursos) r.codigo: r.nome};

    return Scaffold(
      appBar: AppBar(
        title: const Text('Planos'),
        actions: [IconButton(tooltip: 'Atualizar', icon: const Icon(Icons.refresh), onPressed: _carregar)],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _recursos.isEmpty ? null : () => _editar(),
        backgroundColor: CoresPkLots.azulMarinho,
        foregroundColor: Colors.white,
        icon: const Icon(Icons.add),
        label: const Text('Novo plano'),
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 820),
          child: ListView(padding: const EdgeInsets.fromLTRB(16, 16, 16, 88), children: [
            const Text(
                'Mudanças em limites e recursos valem na hora para todas as empresas do plano. '
                'Baixar um limite não apaga nada: só impede criar além dele.',
                style: TextStyle(color: CoresPkLots.textoSecundario)),
            const SizedBox(height: 12),
            if (_erro != null) MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
            if (planos == null && _erro == null) const Center(child: CircularProgressIndicator()),
            for (final plano in planos ?? <Plano>[])
              Card(
                child: ListTile(
                  onTap: () => _editar(plano),
                  title: Row(children: [
                    Flexible(
                      child: Text(plano.nome,
                          style: TextStyle(
                              fontWeight: FontWeight.w700, color: plano.ativo ? CoresPkLots.azulMarinho : CoresPkLots.textoSuave)),
                    ),
                    const SizedBox(width: 8),
                    Text('${Formatadores.moeda(plano.precoPorPatio)}/pátio',
                        style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.textoSecundario)),
                    if (!plano.ativo) ...[
                      const SizedBox(width: 8),
                      const Chip(label: Text('Inativo'), visualDensity: VisualDensity.compact),
                    ],
                  ]),
                  subtitle: Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text([
                      [
                        _limite(plano.maximoPatios, 'pátio(s)'),
                        _limite(plano.maximoVagasPorPatio, 'vagas/pátio'),
                        _limite(plano.maximoUsuarios, 'usuário(s)'),
                        'carência de ${plano.diasCarencia} dia(s)',
                      ].join(' · '),
                      plano.recursos.isEmpty ? 'Sem recursos extras' : plano.recursos.map((c) => nomes[c] ?? c).join(', '),
                      '${plano.empresas} empresa(s)',
                    ].join('\n')),
                  ),
                  isThreeLine: true,
                  trailing: const Icon(Icons.edit_outlined),
                ),
              ),
          ]),
        ),
      ),
    );
  }
}

/// Cria ou edita um plano
class _EditorPlano extends StatefulWidget {
  const _EditorPlano({this.plano, required this.recursos});

  final Plano? plano;
  final List<RecursoPlano> recursos;

  @override
  State<_EditorPlano> createState() => _EditorPlanoState();
}

class _EditorPlanoState extends State<_EditorPlano> {
  final _formulario = GlobalKey<FormState>();
  late final _nome = TextEditingController(text: widget.plano?.nome);
  late final _descricao = TextEditingController(text: widget.plano?.descricao);
  late final _preco = TextEditingController(text: widget.plano == null ? '' : _decimal(widget.plano!.precoPorPatio));
  late final _patios = TextEditingController(text: widget.plano?.maximoPatios?.toString() ?? '');
  late final _vagas = TextEditingController(text: widget.plano?.maximoVagasPorPatio?.toString() ?? '');
  late final _usuarios = TextEditingController(text: widget.plano?.maximoUsuarios?.toString() ?? '');
  late final _carencia = TextEditingController(text: '${widget.plano?.diasCarencia ?? 7}');
  late final Set<String> _recursos = {...?widget.plano?.recursos};
  late bool _ativo = widget.plano?.ativo ?? true;
  bool _salvando = false;
  String? _erro;

  static String _decimal(double v) => v.toStringAsFixed(2).replaceAll('.', ',');

  @override
  void dispose() {
    for (final c in [_nome, _descricao, _preco, _patios, _vagas, _usuarios, _carencia]) {
      c.dispose();
    }
    super.dispose();
  }

  int? _inteiro(TextEditingController c) => int.tryParse(c.text.trim());

  Future<void> _salvar() async {
    if (!_formulario.currentState!.validate()) return;
    setState(() {
      _salvando = true;
      _erro = null;
    });
    try {
      final plano = Plano(
        id: widget.plano?.id ?? '',
        nome: _nome.text.trim(),
        descricao: _descricao.text.trim().isEmpty ? null : _descricao.text.trim(),
        precoPorPatio: lerReais(_preco.text)!,
        maximoPatios: _inteiro(_patios),
        maximoVagasPorPatio: _inteiro(_vagas),
        maximoUsuarios: _inteiro(_usuarios),
        recursos: [for (final r in widget.recursos) if (_recursos.contains(r.codigo)) r.codigo],
        diasCarencia: _inteiro(_carencia)!,
        ativo: _ativo,
      );
      await context.read<ClienteApi>().salvarPlano(plano, id: widget.plano?.id);
      if (mounted) Navigator.of(context).pop(true);
    } on ExcecaoApi catch (e) {
      setState(() => _erro = e.mensagem);
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  // Vazio = sem limite
  String? _validarLimite(String? v) {
    if (v == null || v.trim().isEmpty) return null;
    final n = int.tryParse(v.trim());
    return n == null || n < 1 ? 'Mínimo 1' : null;
  }

  Widget _campoLimite(TextEditingController controle, String rotulo) => Expanded(
        child: TextFormField(
          controller: controle,
          decoration: InputDecoration(labelText: rotulo, hintText: 'Sem limite'),
          keyboardType: TextInputType.number,
          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
          validator: _validarLimite,
        ),
      );

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text(widget.plano == null ? 'Novo plano' : 'Editar plano'),
        content: SizedBox(
          width: 480,
          child: Form(
            key: _formulario,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Expanded(
                    flex: 3,
                    child: TextFormField(
                      controller: _nome,
                      autofocus: widget.plano == null,
                      decoration: const InputDecoration(labelText: 'Nome do plano'),
                      validator: (v) => Validadores.obrigatorio(v, 'o nome'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 2,
                    child: TextFormField(
                      controller: _preco,
                      decoration: const InputDecoration(labelText: 'Preço por pátio/mês', prefixText: 'R\$ '),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      validator: (v) {
                        final valor = lerReais(v ?? '');
                        return valor == null || valor < 0 ? 'Valor inválido' : null;
                      },
                    ),
                  ),
                ]),
                const SizedBox(height: 12),
                TextFormField(controller: _descricao, decoration: const InputDecoration(labelText: 'Descrição (opcional)')),
                const SizedBox(height: 20),
                const TituloSecao('Limites', icone: Icons.tune),
                Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  _campoLimite(_patios, 'Pátios'),
                  const SizedBox(width: 12),
                  _campoLimite(_vagas, 'Vagas por pátio'),
                  const SizedBox(width: 12),
                  _campoLimite(_usuarios, 'Usuários'),
                ]),
                const SizedBox(height: 4),
                const Text('Deixe em branco para não limitar. Usuários contam administradores e operadores ativos.',
                    style: TextStyle(fontSize: 12, color: CoresPkLots.textoSuave)),
                const SizedBox(height: 20),
                const TituloSecao('Recursos liberados', icone: Icons.verified_outlined),
                for (final recurso in widget.recursos)
                  CheckboxListTile(
                    value: _recursos.contains(recurso.codigo),
                    contentPadding: EdgeInsets.zero,
                    controlAffinity: ListTileControlAffinity.leading,
                    title: Text(recurso.nome),
                    subtitle: Text(recurso.descricao),
                    onChanged: (marcado) => setState(() => marcado == true ? _recursos.add(recurso.codigo) : _recursos.remove(recurso.codigo)),
                  ),
                const SizedBox(height: 12),
                const TituloSecao('Cobrança', icone: Icons.receipt_long_outlined),
                TextFormField(
                  controller: _carencia,
                  decoration: const InputDecoration(
                    labelText: 'Carência após o vencimento (dias)',
                    helperText: 'Depois disso, a empresa entra em bloqueio parcial até pagar.',
                  ),
                  keyboardType: TextInputType.number,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  validator: (v) {
                    final n = int.tryParse(v ?? '');
                    return n == null || n > 60 ? '0 a 60' : null;
                  },
                ),
                SwitchListTile(
                  value: _ativo,
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Disponível para novas assinaturas'),
                  subtitle: const Text('Inativo: quem já usa continua no plano'),
                  onChanged: (v) => setState(() => _ativo = v),
                ),
                if (_erro != null) ...[
                  const SizedBox(height: 8),
                  Text(_erro!, style: const TextStyle(color: CoresPkLots.erro)),
                ],
              ]),
            ),
          ),
        ),
        actions: [
          TextButton(onPressed: _salvando ? null : () => Navigator.of(context).pop(false), child: const Text('Cancelar')),
          SizedBox(
            width: 120,
            child: FilledButton(
              onPressed: _salvando ? null : _salvar,
              child: _salvando
                  ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Salvar'),
            ),
          ),
        ],
      );
}
