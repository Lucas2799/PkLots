import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../modelos/modelos.dart';
import '../../servicos/cliente_api.dart';
import '../../servicos/excecao_api.dart';
import '../../tema/cores.dart';
import '../../util/formatadores.dart';
import '../../widgets/estados_tela.dart';
import '../../widgets/fatura_widgets.dart';

/// Plano contratado pela empresa: valor, vencimento, uso dos limites e recursos liberados
class AbaAssinatura extends StatefulWidget {
  const AbaAssinatura({super.key});

  @override
  State<AbaAssinatura> createState() => _AbaAssinaturaState();
}

class _AbaAssinaturaState extends State<AbaAssinatura> {
  MinhaAssinatura? _assinatura;
  List<Fatura> _faturas = [];
  String? _erro;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _carregar());
  }

  Future<void> _carregar() async {
    try {
      final api = context.read<ClienteApi>();
      final resultados = await Future.wait([api.minhaAssinatura(), api.minhasFaturas()]);
      if (mounted) {
        setState(() {
          _assinatura = resultados[0] as MinhaAssinatura;
          _faturas = resultados[1] as List<Fatura>;
          _erro = null;
        });
      }
    } on ExcecaoApi catch (e) {
      if (mounted) setState(() => _erro = e.mensagem);
    }
  }

  @override
  Widget build(BuildContext context) {
    final a = _assinatura;
    if (a == null) {
      return Padding(
        padding: const EdgeInsets.all(16),
        child: _erro == null ? const Center(child: CircularProgressIndicator()) : MensagemErro(mensagem: _erro!, aoTentarNovamente: _carregar),
      );
    }

    final hoje = DateTime.now();
    final emTeste = a.testeAte != null && !a.testeAte!.isBefore(DateTime(hoje.year, hoje.month, hoje.day));

    return RefreshIndicator(
      onRefresh: _carregar,
      child: ListView(padding: const EdgeInsets.all(16), children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                const Icon(Icons.workspace_premium_outlined, color: CoresPkLots.azulMarinho),
                const SizedBox(width: 8),
                Expanded(
                  child: Text('Plano ${a.nomePlano}',
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                ),
              ]),
              if (a.descricaoPlano != null) ...[
                const SizedBox(height: 4),
                Text(a.descricaoPlano!, style: const TextStyle(color: CoresPkLots.textoSecundario)),
              ],
              const SizedBox(height: 16),
              Text(Formatadores.moeda(a.valorMensal),
                  style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
              Text('por mês · ${Formatadores.moeda(a.precoPorPatio)} por pátio ativo · vence todo dia ${a.diaVencimento}',
                  style: const TextStyle(color: CoresPkLots.textoSecundario)),
              if (emTeste) ...[
                const SizedBox(height: 12),
                Row(children: [
                  const Icon(Icons.card_giftcard_outlined, size: 18, color: CoresPkLots.disponivel),
                  const SizedBox(width: 6),
                  Text('Período de teste até ${Formatadores.data(a.testeAte!)}',
                      style: const TextStyle(color: CoresPkLots.disponivel, fontWeight: FontWeight.w600)),
                ]),
              ],
            ]),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const TituloSecao('Faturas', icone: Icons.request_quote_outlined),
              if (_faturas.isEmpty)
                const Text('Nenhuma fatura até agora.', style: TextStyle(color: CoresPkLots.textoSuave)),
              for (final (i, f) in _faturas.indexed) ...[
                if (i > 0) const Divider(height: 1),
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(f.descricao),
                  subtitle: Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Wrap(spacing: 12, runSpacing: 4, children: [
                      SituacaoFatura(fatura: f),
                      Text(f.pagaEm != null ? 'Paga em ${Formatadores.data(f.pagaEm!.toLocal())}' : 'Vence ${Formatadores.data(f.vencimento)}'),
                    ]),
                  ),
                  trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
                    Text(Formatadores.moeda(f.valor), style: const TextStyle(fontWeight: FontWeight.w700, color: CoresPkLots.azulMarinho)),
                    if (f.emAberto && f.pixCopiaECola != null)
                      TextButton.icon(
                        style: TextButton.styleFrom(visualDensity: VisualDensity.compact, padding: EdgeInsets.zero),
                        onPressed: () => mostrarPixFatura(context, f),
                        icon: const Icon(Icons.qr_code_2, size: 18),
                        label: const Text('Pagar com PIX'),
                      ),
                  ]),
                ),
              ],
            ]),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const TituloSecao('Uso do plano', icone: Icons.tune),
              _Limite(rotulo: 'Pátios ativos', uso: a.patios),
              _Limite(rotulo: 'Usuários ativos (administradores e operadores)', uso: a.usuarios),
              _Limite(rotulo: 'Vagas no maior pátio', uso: a.vagasNoMaiorPatio),
            ]),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const TituloSecao('Recursos', icone: Icons.verified_outlined),
              for (final r in a.recursos)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  leading: Icon(r.liberado ? Icons.check_circle : Icons.remove_circle_outline,
                      color: r.liberado ? CoresPkLots.disponivel : CoresPkLots.textoSuave),
                  title: Text(r.nome, style: TextStyle(color: r.liberado ? null : CoresPkLots.textoSuave)),
                  trailing: r.liberado ? null : const Text('Não incluído', style: TextStyle(color: CoresPkLots.textoSuave)),
                ),
              const SizedBox(height: 8),
              const Text('Para mudar de plano, fale com o suporte do PkLots.', style: TextStyle(color: CoresPkLots.textoSecundario)),
            ]),
          ),
        ),
      ]),
    );
  }
}

/// Uso de um limite: "2 de 3", com barra (ou "sem limite")
class _Limite extends StatelessWidget {
  const _Limite({required this.rotulo, required this.uso});

  final String rotulo;
  final UsoLimite uso;

  @override
  Widget build(BuildContext context) {
    final maximo = uso.maximo;
    final noLimite = maximo != null && uso.usado >= maximo;
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Expanded(child: Text(rotulo)),
          Text(maximo == null ? '${uso.usado} · sem limite' : '${uso.usado} de $maximo',
              style: TextStyle(fontWeight: FontWeight.w600, color: noLimite ? CoresPkLots.manutencao : CoresPkLots.azulMarinho)),
        ]),
        if (maximo != null) ...[
          const SizedBox(height: 6),
          LinearProgressIndicator(
            value: (uso.usado / maximo).clamp(0, 1).toDouble(),
            minHeight: 6,
            borderRadius: BorderRadius.circular(3),
            color: noLimite ? CoresPkLots.manutencao : CoresPkLots.grafico,
            backgroundColor: CoresPkLots.borda,
          ),
        ],
      ]),
    );
  }
}
