import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:pklots/modelos/modelos.dart';
import 'package:pklots/servicos/relatorios_pdf.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() => initializeDateFormatting('pt_BR'));

  bool ehPdf(List<int> bytes) => ascii.decode(bytes.take(5).toList()) == '%PDF-';

  final dias = [
    for (var d = 1; d <= 30; d++)
      {'data': '2026-09-${d.toString().padLeft(2, '0')}', 'entradas': d, 'saidas': d, 'receitaAvulsos': d * 15.0, 'receitaMensalidades': 0.0},
  ];

  test('relatório do dono em PDF, com várias páginas de movimento', () async {
    final relatorio = RelatorioEmpresa.deJson({
      'de': '2026-09-01', 'ate': '2026-09-30', 'nomeEmpresa': 'Estacionamentos São João', 'nomePatio': null,
      'totais': {
        'entradas': 465, 'saidas': 465, 'saidasCobradas': 460, 'saidasIsentas': 3, 'saidasMensalistas': 2, 'cancelamentos': 1,
        'receitaAvulsos': 6975.0, 'receitaMensalidades': 300.0, 'receitaTotal': 7275.0, 'ticketMedio': 15.16, 'permanenciaMedia': '1h 5m',
      },
      'porForma': [{'forma': 'cartao', 'quantidade': 460, 'total': 6975.0}],
      'porDia': dias,
      'entradasPorHora': [for (var h = 0; h < 24; h++) {'hora': h, 'entradas': h}],
      'porPatio': [
        {'id': 'p1', 'nome': 'Centro', 'ativo': true, 'vagas': 30, 'entradas': 400, 'saidas': 400, 'receitaAvulsos': 6000.0,
          'receitaMensalidades': 300.0, 'giroPorVaga': 0.44},
        {'id': 'p2', 'nome': 'Praça', 'ativo': false, 'vagas': 10, 'entradas': 65, 'saidas': 65, 'receitaAvulsos': 975.0,
          'receitaMensalidades': 0.0, 'giroPorVaga': 0.22},
      ],
      'porOperador': [
        {'id': 'u1', 'nome': 'Bruno Lima', 'saidas': 460, 'mensalidades': 1, 'recebido': 7275.0, 'caixasFechados': 30,
          'diferencaCaixa': -5.5, 'cancelamentos': 1},
      ],
      'cancelamentos': [
        {'canceladaEm': '2026-09-30T12:00:00Z', 'placa': 'BRA2E19', 'nomePatio': 'Centro', 'canceladaPor': null, 'motivo': 'Duplicada'},
      ],
      'mensalistas': {'ativos': 3, 'emDia': 2, 'vencidos': 1, 'novos': 0, 'pagamentos': 1},
    });

    expect(relatorio.entradasPorHora[23], 23);
    expect(ehPdf(await RelatoriosPdf.gerarEmpresa(relatorio)), isTrue);
  });

  test('relatório da plataforma em PDF', () async {
    final relatorio = RelatorioPlataforma.deJson({
      'de': '2026-09-01', 'ate': '2026-09-30',
      'totais': {
        'empresasAtivas': 1, 'empresasNovas': 0, 'empresasSemMovimento': 0, 'patiosAtivos': 1, 'vagas': 30, 'usuariosAtivos': 2,
        'mensalistasAtivos': 3, 'entradas': 465, 'valorProcessado': 7275.0,
      },
      'porDia': dias,
      'empresas': [
        {'id': 'e1', 'nome': 'Estacionamentos São João', 'ativa': true, 'criadoEm': '2026-08-01T12:00:00Z', 'patios': 1, 'vagas': 30,
          'usuarios': 2, 'mensalistasAtivos': 3, 'entradas': 465, 'valorProcessado': 7275.0, 'ultimaEntrada': null},
      ],
    });

    expect(relatorio.periodo.dias, 30);
    expect(ehPdf(await RelatoriosPdf.gerarPlataforma(relatorio)), isTrue);
  });
}
