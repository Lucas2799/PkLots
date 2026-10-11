import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'estado/sessao_provider.dart';
import 'tema/tema_pklots.dart';
import 'telas/acesso/ativar_conta_tela.dart';
import 'telas/acesso/convite_tela.dart';
import 'telas/acesso/redefinir_senha_tela.dart';
import 'telas/gestao/painel_tela.dart';
import 'telas/login_tela.dart';
import 'telas/troca_senha_tela.dart';
import 'telas/plataforma/plataforma_tela.dart';
import 'telas/motorista/inicio_motorista_tela.dart';
import 'widgets/logo_pklots.dart';

class PkLotsApp extends StatelessWidget {
  const PkLotsApp({super.key, this.enderecoInicial});

  /// Endereço com que o app foi aberto (web): links dos e-mails trazem ?convite= ou ?redefinir=
  final Uri? enderecoInicial;

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'PkLots',
        debugShowCheckedModeBanner: false,
        theme: TemaPkLots.claro,
        locale: const Locale('pt', 'BR'),
        supportedLocales: const [Locale('pt', 'BR')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        home: _Raiz(enderecoInicial: enderecoInicial),
      );
}

/// Decide a tela inicial: link recebido por e-mail (convite de mensalista, ativação de conta, nova senha)
/// e, depois, pela sessão:
/// login, painel de gestão, plataforma ou app do mensalista
class _Raiz extends StatefulWidget {
  const _Raiz({this.enderecoInicial});

  final Uri? enderecoInicial;

  @override
  State<_Raiz> createState() => _RaizState();
}

class _RaizState extends State<_Raiz> {
  late String? _convite = widget.enderecoInicial?.queryParameters['convite'];
  late String? _redefinir = widget.enderecoInicial?.queryParameters['redefinir'];
  late String? _ativar = widget.enderecoInicial?.queryParameters['ativar'];

  // Tira o token do endereço (não fica no histórico do navegador nem volta no F5)
  void _concluirLink() {
    setState(() => _convite = _redefinir = _ativar = null);
    SystemNavigator.routeInformationUpdated(uri: Uri(path: '/'), replace: true);
  }

  @override
  Widget build(BuildContext context) {
    final sessao = context.watch<SessaoProvider>();
    if (sessao.situacao != SituacaoSessao.carregando) {
      if (_convite case final token?) return ConviteTela(token: token, aoConcluir: _concluirLink);
      if (_redefinir case final token?) return RedefinirSenhaTela(token: token, aoConcluir: _concluirLink);
      if (_ativar case final token?) return AtivarContaTela(token: token, aoConcluir: _concluirLink);
    }
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
