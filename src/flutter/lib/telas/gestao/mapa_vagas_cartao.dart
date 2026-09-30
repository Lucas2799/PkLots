import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/gestao_provider.dart';
import '../../modelos/modelos.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/recibo_dialogo.dart';
import '../../widgets/situacao_vaga.dart';

/// Mapa das vagas por fileira; toque abre as ações da vaga
class MapaVagasCartao extends StatelessWidget {
  const MapaVagasCartao({super.key, required this.vagas});

  final List<Vaga> vagas;

  @override
  Widget build(BuildContext context) {
    final fileiras = <String, List<Vaga>>{};
    for (final vaga in vagas) {
      fileiras.putIfAbsent(vaga.fileira, () => []).add(vaga);
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const TituloSecao('Mapa de vagas', icone: Icons.grid_view),
          const Wrap(spacing: 8, runSpacing: 8, children: [
            EtiquetaSituacao(situacao: Vaga.disponivel),
            EtiquetaSituacao(situacao: Vaga.ocupada),
            EtiquetaSituacao(situacao: Vaga.manutencao),
          ]),
          const SizedBox(height: 16),
          for (final MapEntry(key: fileira, value: lista) in fileiras.entries) ...[
            Text('Fileira $fileira',
                style: const TextStyle(fontWeight: FontWeight.w600, color: CoresPkLots.textoSecundario)),
            const SizedBox(height: 8),
            Wrap(spacing: 6, runSpacing: 6, children: [for (final vaga in lista) _BlocoVaga(vaga: vaga)]),
            const SizedBox(height: 14),
          ],
        ]),
      ),
    );
  }
}

class _BlocoVaga extends StatelessWidget {
  const _BlocoVaga({required this.vaga});

  final Vaga vaga;

  @override
  Widget build(BuildContext context) {
    final aparencia = AparenciaSituacao.de(vaga.situacao);
    final ocupada = vaga.situacao == Vaga.ocupada;
    final descricao = [
      'Vaga ${vaga.numero}',
      aparencia.rotulo,
      if (vaga.placaAtual != null) Formatadores.placa(vaga.placaAtual!),
    ].join(' · ');

    return Tooltip(
      message: descricao,
      child: Semantics(
        button: true,
        label: descricao,
        excludeSemantics: true,
        child: InkWell(
          borderRadius: BorderRadius.circular(8),
          onTap: () => _mostrarAcoes(context, vaga),
          child: Container(
            width: 58,
            height: 58,
            decoration: BoxDecoration(
              color: ocupada ? aparencia.cor : aparencia.cor.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: aparencia.cor.withValues(alpha: ocupada ? 1 : 0.5)),
            ),
            child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
              Icon(aparencia.icone, size: 18, color: ocupada ? Colors.white : aparencia.cor),
              const SizedBox(height: 2),
              Text(vaga.numero,
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                    color: ocupada ? Colors.white : CoresPkLots.azulMarinho,
                  )),
            ]),
          ),
        ),
      ),
    );
  }

  Future<void> _mostrarAcoes(BuildContext context, Vaga vaga) async {
    final estado = context.read<GestaoProvider>();
    await showModalBottomSheet(
      context: context,
      showDragHandle: true,
      builder: (contextoFolha) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Row(children: [
              Text('Vaga ${vaga.numero}',
                  style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
              const SizedBox(width: 12),
              EtiquetaSituacao(situacao: vaga.situacao),
            ]),
            const SizedBox(height: 12),
            if (vaga.placaAtual != null)
              Text('Veículo: ${Formatadores.placa(vaga.placaAtual!)}'
                  '${vaga.ocupadaDesde != null ? ' · desde ${Formatadores.hora(vaga.ocupadaDesde!)}' : ''}'),
            if (vaga.observacoes != null) Text('Observações: ${vaga.observacoes}'),
            const SizedBox(height: 16),
            switch (vaga.situacao) {
              Vaga.disponivel => FilledButton.icon(
                  style: FilledButton.styleFrom(backgroundColor: CoresPkLots.manutencao),
                  icon: const Icon(Icons.build_outlined),
                  label: const Text('Marcar manutenção'),
                  onPressed: () async {
                    final observacoes = await _pedirObservacoes(contextoFolha);
                    if (observacoes == null || !contextoFolha.mounted) return;
                    Navigator.of(contextoFolha).pop();
                    await _executar(context, () => estado.marcarManutencao(vaga.id, observacoes),
                        'Vaga ${vaga.numero} em manutenção');
                  },
                ),
              Vaga.manutencao => FilledButton.icon(
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('Liberar vaga'),
                  onPressed: () async {
                    Navigator.of(contextoFolha).pop();
                    await _executar(context, () => estado.liberarManutencao(vaga.id), 'Vaga ${vaga.numero} liberada');
                  },
                ),
              _ => FilledButton.icon(
                  icon: const Icon(Icons.logout),
                  label: const Text('Registrar saída'),
                  onPressed: vaga.placaAtual == null
                      ? null
                      : () async {
                          Navigator.of(contextoFolha).pop();
                          try {
                            final saida = await estado.registrarSaida(vaga.placaAtual!);
                            if (context.mounted) await mostrarRecibo(context, saida);
                          } on ExcecaoApi catch (e) {
                            if (context.mounted) mostrarAviso(context, e.mensagem, erro: true);
                          }
                        },
                ),
            },
          ]),
        ),
      ),
    );
  }

  Future<void> _executar(BuildContext context, Future<Object?> Function() acao, String sucesso) async {
    try {
      await acao();
      if (context.mounted) mostrarAviso(context, sucesso);
    } on ExcecaoApi catch (e) {
      if (context.mounted) mostrarAviso(context, e.mensagem, erro: true);
    }
  }

  /// Retorna o texto (pode ser vazio) ou null se cancelou
  Future<String?> _pedirObservacoes(BuildContext context) {
    final controle = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Motivo da manutenção'),
        content: TextField(
          controller: controle,
          autofocus: true,
          decoration: const InputDecoration(hintText: 'Ex.: pintura, lâmpada queimada (opcional)'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
          TextButton(onPressed: () => Navigator.of(context).pop(controle.text.trim()), child: const Text('Confirmar')),
        ],
      ),
    );
  }
}
