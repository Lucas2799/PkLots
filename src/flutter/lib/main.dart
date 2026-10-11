import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'estado/administracao_provider.dart';
import 'estado/gestao_provider.dart';
import 'estado/motorista_provider.dart';
import 'estado/sessao_provider.dart';
import 'servicos/armazenamento_sessao.dart';
import 'servicos/cliente_api.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('pt_BR');

  final api = ClienteApi();
  final sessao = SessaoProvider(api, ArmazenamentoSessao());
  await sessao.restaurar();

  runApp(MultiProvider(
    providers: [
      Provider.value(value: api),
      ChangeNotifierProvider.value(value: sessao),
      ChangeNotifierProvider(create: (_) => MotoristaProvider(api)),
      ChangeNotifierProvider(create: (_) => GestaoProvider(api)),
      ChangeNotifierProvider(create: (_) => AdministracaoProvider(api)),
    ],
    child: PkLotsApp(enderecoInicial: Uri.base),
  ));
}
