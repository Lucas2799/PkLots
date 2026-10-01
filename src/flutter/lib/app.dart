import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'estado/sessao_provider.dart';
import 'tema/tema_pklots.dart';
import 'telas/gestao/painel_tela.dart';
import 'telas/login_tela.dart';
import 'telas/troca_senha_tela.dart';
import 'telas/plataforma/plataforma_tela.dart';
import 'telas/motorista/inicio_motorista_tela.dart';
import 'widgets/logo_pklots.dart';

class PkLotsApp extends StatelessWidget {
  const PkLotsApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'PkLots',
        debugShowCheckedModeBanner: false,
        theme: TemaPkLots.claro,
        locale: const Locale('pt', 'BR'),
        supportedLocales: const [Locale('pt', 'BR')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        home: const _Raiz(),
      );
}

/// Decide a tela inicial pela sessão: login, painel de gestão ou app do motorista
class _Raiz extends StatelessWidget {
  const _Raiz();

  @override
  Widget build(BuildContext context) {
    final sessao = context.watch<SessaoProvider>();
    return switch (sessao.situacao) {
      SituacaoSessao.carregando => const Scaffold(body: Center(child: LogoPkLots(tamanho: 180))),
      SituacaoSessao.deslogado => const LoginTela(),
      SituacaoSessao.logado when sessao.deveTrocarSenha => const TrocaSenhaTela(obrigatoria: true),
      SituacaoSessao.logado => sessao.ehSuperAdmin
          ? const PlataformaTela()
          : sessao.ehGestao
              ? const PainelTela()
              : const InicioMotoristaTela(),
    };
  }
}
