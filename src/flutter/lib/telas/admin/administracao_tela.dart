import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../estado/administracao_provider.dart';
import '../../estado/gestao_provider.dart';
import '../../tema/cores.dart';
import 'aba_pagamentos.dart';
import 'aba_patios.dart';
import '../../widgets/estados_tela.dart';
import 'aba_impressao.dart';
import 'aba_preco.dart';
import 'aba_usuarios.dart';
import 'aba_vagas.dart';

/// Área exclusiva do administrador (a API também exige o perfil admin)
class AdministracaoTela extends StatefulWidget {
  const AdministracaoTela({super.key});

  @override
  State<AdministracaoTela> createState() => _AdministracaoTelaState();
}

class _AdministracaoTelaState extends State<AdministracaoTela> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<AdministracaoProvider>().carregar());
  }

  @override
  Widget build(BuildContext context) {
    final estado = context.watch<AdministracaoProvider>();
    final configuracao = estado.configuracao;

    final patioAtual = context.watch<GestaoProvider>().patioAtual;

    return DefaultTabController(
      length: 6,
      child: Scaffold(
        appBar: AppBar(
          title: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Administração'),
            if (patioAtual != null)
              Text('Configurando: ${patioAtual.nome}',
                  style: const TextStyle(fontSize: 12, color: CoresPkLots.textoSecundario)),
          ]),
          bottom: const TabBar(isScrollable: true, tabAlignment: TabAlignment.center, tabs: [
            Tab(icon: Icon(Icons.store_mall_directory_outlined), text: 'Pátios'),
            Tab(icon: Icon(Icons.payments_outlined), text: 'Preço'),
            Tab(icon: Icon(Icons.local_parking), text: 'Vagas'),
            Tab(icon: Icon(Icons.people_outline), text: 'Usuários'),
            Tab(icon: Icon(Icons.account_balance_outlined), text: 'Pagamentos'),
            Tab(icon: Icon(Icons.print_outlined), text: 'Impressão'),
          ]),
        ),
        body: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 720),
            child: switch ((configuracao, estado.erro)) {
              (null, final String erro) => Padding(
                  padding: const EdgeInsets.all(16),
                  child: MensagemErro(mensagem: erro, aoTentarNovamente: estado.carregar),
                ),
              (null, _) => const Center(child: CircularProgressIndicator()),
              (final config?, _) => TabBarView(children: [
                  const AbaPatios(),
                  // A chave recria a aba com os valores salvos no servidor
                  AbaPreco(
                      key: ValueKey(('preco', config.valorHora, config.minutosTolerancia, config.nome, config.valorMensalidade,
                          config.vagasReservadasMensalistas)),
                      configuracao: config),
                  AbaVagas(key: ValueKey(('vagas', config.totalVagas)), configuracao: config),
                  const AbaUsuarios(),
                  const AbaPagamentos(),
                  if (estado.impressao case final impressao?)
                    AbaImpressao(key: ValueKey(('impressao', impressao.paraJson().toString())), configuracao: impressao)
                  else
                    const Center(child: CircularProgressIndicator()),
                ]),
            },
          ),
        ),
      ),
    );
  }
}
