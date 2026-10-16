import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/ambiente.dart';
import '../modelos/modelos.dart';
import 'excecao_api.dart';

/// Cliente HTTP da API PkLots
class ClienteApi {
  ClienteApi({http.Client? cliente, String? urlBase})
      : _http = cliente ?? http.Client(),
        _urlBase = urlBase ?? Ambiente.urlApi;

  final http.Client _http;
  final String _urlBase;
  static const _tempoLimite = Duration(seconds: 30);

  String? token;

  /// Pátio escolhido no seletor do painel (enviado em X-Patio-Id; a API valida o acesso)
  String? patioAtualId;

  /// Chamado quando a API responde 401 com um token presente (sessão expirada)
  void Function()? aoExpirarSessao;

  /// Chamado quando a API exige a troca de senha (403 com código "troca_senha")
  void Function()? aoExigirTrocaSenha;

  // ---------- Autenticação ----------

  Future<Token> login(String email, String senha) async =>
      Token.deJson(await _post('/api/auth/login', {'email': email, 'senha': senha}));

  Future<Token> trocarSenha(String senhaAtual, String novaSenha) async =>
      Token.deJson(await _post('/api/auth/trocar-senha', {'senhaAtual': senhaAtual, 'novaSenha': novaSenha}));

  /// Envia o link de redefinição (a API responde igual exista ou não a conta)
  Future<void> esqueciSenha(String email) => _post('/api/auth/esqueci-senha', {'email': email});

  Future<void> redefinirSenhaPorEmail(String token, String novaSenha) =>
      _post('/api/auth/redefinir-senha', {'token': token, 'novaSenha': novaSenha});

  // ---------- Convite de admin/operador (link do e-mail, sem login) ----------

  Future<ConviteUsuario> conviteUsuario(String token) async =>
      ConviteUsuario.deJson(await _get('/api/publico/convites-usuario/${Uri.encodeComponent(token)}'));

  /// Cria a primeira senha e devolve o token (já entra no sistema)
  Future<Token> ativarConta(String token, String senha) async =>
      Token.deJson(await _post('/api/auth/ativar-conta', {'token': token, 'senha': senha}));

  // ---------- Convite de mensalista (link do e-mail, sem login) ----------

  Future<ConviteMensalista> convite(String token) async =>
      ConviteMensalista.deJson(await _get('/api/publico/convites/${Uri.encodeComponent(token)}'));

  /// Conta nova: devolve o token (já entra no app). Conta existente: null (entra com a senha de sempre).
  Future<Token?> aceitarConvite(String token, {String? nomeCompleto, String? senha}) async {
    final json = await _post('/api/publico/convites/${Uri.encodeComponent(token)}/aceitar',
        {'nomeCompleto': nomeCompleto, 'senha': senha}) as Map<String, dynamic>;
    final dados = json['token'];
    return dados == null ? null : Token.deJson(dados as Map<String, dynamic>);
  }

  // ---------- Vagas ----------

  Future<ResumoVagas> resumoVagas() async => ResumoVagas.deJson(await _get('/api/vagas/resumo'));

  Future<List<Vaga>> vagas() async => _lista(await _get('/api/vagas'), Vaga.deJson);

  Future<Vaga> marcarManutencao(String vagaId, String? observacoes) async =>
      Vaga.deJson(await _enviar('PUT', '/api/vagas/$vagaId/manutencao', {'observacoes': observacoes}));

  Future<Vaga> liberarManutencao(String vagaId) async =>
      Vaga.deJson(await _enviar('DELETE', '/api/vagas/$vagaId/manutencao', null));

  // ---------- Mensalista (app) ----------

  Future<List<PatioDisponibilidade>> patiosParaMotorista() async =>
      _lista(await _get('/api/motorista/patios'), PatioDisponibilidade.deJson);

  Future<List<ContratoMensalista>> meusContratos() async =>
      _lista(await _get('/api/motorista/contratos'), ContratoMensalista.deJson);

  Future<List<ItemHistorico>> meuHistorico() async =>
      _lista(await _get('/api/motorista/historico'), ItemHistorico.deJson);

  // ---------- Gestão ----------

  Future<List<PatioResumo>> patiosPermitidos() async => _lista(await _get('/api/patios'), PatioResumo.deJson);

  Future<Dashboard> dashboard() async => Dashboard.deJson(await _get('/api/dashboard'));

  Future<List<ItemHistorico>> historicoVeiculo(String placa) async =>
      _lista(await _get('/api/veiculos/${Uri.encodeComponent(placa)}/historico'), ItemHistorico.deJson);

  Future<Entrada> registrarEntrada(String placa) async =>
      Entrada.deJson(await _post('/api/entradas/', {'placa': placa, 'confianca': 1, 'metodoReconhecimento': 'manual'}));

  /// Cancela uma entrada ativa (204); operador só logo após a entrada, admin a qualquer momento
  Future<void> cancelarEntrada(String entradaId, String motivo) =>
      _post('/api/entradas/$entradaId/cancelar', {'motivo': motivo});

  // Saída em dois passos: cobrança (valor congelado) e confirmação do pagamento

  Future<CobrancaSaida> cobrarSaida(String placa) async =>
      CobrancaSaida.deJson(await _post('/api/saidas/cobranca', {'placa': placa}));

  /// Pela leitura do ticket: aceita "PKLOTS:K7M2Q9XD", "K7M2-Q9XD" etc.
  Future<CobrancaSaida> cobrarSaidaPorTicket(String codigo) async =>
      CobrancaSaida.deJson(await _post('/api/saidas/cobranca', {'codigoTicket': codigo}));

  Future<CobrancaSaida> cobrarSaidaPorImagem(List<int> bytes, String nomeArquivo) async =>
      CobrancaSaida.deJson(await _enviarImagem('/api/saidas/cobranca/imagem', bytes, nomeArquivo));

  Future<Saida> confirmarSaida(String cobrancaId, {String? forma, double? valorRecebido}) async => Saida.deJson(
      await _post('/api/saidas/cobranca/$cobrancaId/confirmar', {'formaPagamento': forma, 'valorRecebido': valorRecebido}));

  // ---------- Caixa ----------

  Future<Caixa?> caixaAtual() async {
    final json = await _get('/api/caixa/atual');
    return json == null ? null : Caixa.deJson(json);
  }

  Future<Caixa> abrirCaixa(double trocoInicial) async =>
      Caixa.deJson(await _post('/api/caixa/abrir', {'trocoInicial': trocoInicial}));

  Future<Caixa> movimentarCaixa(String tipo, double valor, String? motivo) async =>
      Caixa.deJson(await _post('/api/caixa/movimentos', {'tipo': tipo, 'valor': valor, 'motivo': motivo}));

  Future<Caixa> fecharCaixa(double dinheiroContado, String? observacao) async =>
      Caixa.deJson(await _post('/api/caixa/fechar', {'dinheiroContado': dinheiroContado, 'observacao': observacao}));

  Future<List<Caixa>> caixasDoDia() async => _lista(await _get('/api/caixa/'), Caixa.deJson);

  Future<Entrada> registrarEntradaPorImagem(List<int> bytes, String nomeArquivo) async =>
      Entrada.deJson(await _enviarImagem('/api/entradas/imagem', bytes, nomeArquivo));


  // ---------- Impressão ----------

  Future<TicketEntrada> ticketEntrada(String entradaId) async =>
      TicketEntrada.deJson(await _get('/api/entradas/$entradaId/ticket'));

  /// Imprime na impressora de rede (409 se não houver impressora de rede configurada)
  Future<void> imprimirTicketEntrada(String entradaId) => _post('/api/entradas/$entradaId/imprimir', null);

  Future<ConfiguracaoImpressao> configuracaoImpressao() async =>
      ConfiguracaoImpressao.deJson(await _get('/api/admin/impressao'));

  Future<ConfiguracaoImpressao> salvarConfiguracaoImpressao(ConfiguracaoImpressao configuracao) async =>
      ConfiguracaoImpressao.deJson(await _enviar('PUT', '/api/admin/impressao', configuracao.paraJson()));

  Future<void> imprimirTeste() => _post('/api/admin/impressao/teste', null);

  // ---------- Administração (somente admin) ----------

  Future<ConfiguracaoPagamento> configuracaoPagamento() async =>
      ConfiguracaoPagamento.deJson(await _get('/api/admin/pagamentos'));

  Future<ConfiguracaoPagamento> salvarConfiguracaoPagamento(ConfiguracaoPagamento c) async =>
      ConfiguracaoPagamento.deJson(await _enviar('PUT', '/api/admin/pagamentos', c.paraJson()));

  Future<String> pixDeTeste() async =>
      ((await _post('/api/admin/pagamentos/pix-teste', null)) as Map<String, dynamic>)['pixCopiaECola'] as String;

  // ---------- Tabela de preço (pátio escolhido no painel) ----------

  Future<TabelaPreco> tabelaPreco() async => TabelaPreco.deJson(await _get('/api/admin/tabela-preco'));

  Future<TabelaPreco> salvarTabelaPreco(RegrasPreco semana, RegrasPreco? fimDeSemana, {int? minutosTolerancia}) async =>
      TabelaPreco.deJson(await _enviar('PUT', '/api/admin/tabela-preco', {
        'semana': semana.paraJson(),
        'fimDeSemana': fimDeSemana?.paraJson(),
        'minutosTolerancia': minutosTolerancia,
      }));

  Future<SimulacaoTarifa> simularTarifa(RegrasPreco semana, RegrasPreco? fimDeSemana, DateTime entrada, DateTime saida,
          {int? minutosTolerancia}) async =>
      SimulacaoTarifa.deJson(await _post('/api/admin/tabela-preco/simular', {
        'semana': semana.paraJson(),
        'fimDeSemana': fimDeSemana?.paraJson(),
        'entrada': entrada.toUtc().toIso8601String(),
        'saida': saida.toUtc().toIso8601String(),
        'minutosTolerancia': minutosTolerancia,
      }));

  Future<ConfiguracaoPatio> configuracao() async => ConfiguracaoPatio.deJson(await _get('/api/admin/configuracao'));

  Future<ConfiguracaoPatio> salvarConfiguracao(String nome, double valorHora, int minutosTolerancia,
          {double? valorMensalidade, int? vagasReservadasMensalistas}) async =>
      ConfiguracaoPatio.deJson(await _enviar('PUT', '/api/admin/configuracao', {
        'nome': nome,
        'valorHora': valorHora,
        'minutosTolerancia': minutosTolerancia,
        'valorMensalidade': valorMensalidade,
        'vagasReservadasMensalistas': vagasReservadasMensalistas,
      }));

  Future<ConfiguracaoPatio> alterarQuantidadeVagas(int total) async =>
      ConfiguracaoPatio.deJson(await _enviar('PUT', '/api/admin/vagas/quantidade', {'total': total}));

  Future<List<UsuarioAdmin>> usuarios() async => _lista(await _get('/api/admin/usuarios'), UsuarioAdmin.deJson);

  /// Sem senha: a pessoa recebe um convite por e-mail e cria a dela
  Future<UsuarioAdmin> criarUsuario(String email, String nomeCompleto, String perfil, List<String> patiosIds) async =>
      UsuarioAdmin.deJson(await _post('/api/admin/usuarios',
          {'email': email, 'nomeCompleto': nomeCompleto, 'perfil': perfil, 'patiosIds': patiosIds}));

  Future<UsuarioAdmin> reenviarConviteUsuario(String id) async =>
      UsuarioAdmin.deJson(await _post('/api/admin/usuarios/$id/convite', null));

  /// Envia ao usuário o link para criar uma nova senha (o admin não define a senha de ninguém)
  Future<void> enviarLinkNovaSenha(String id) => _post('/api/admin/usuarios/$id/link-senha', null);

  Future<UsuarioAdmin> atualizarUsuario(String id, String nomeCompleto, String perfil, bool ativo, List<String> patiosIds) async =>
      UsuarioAdmin.deJson(await _enviar('PUT', '/api/admin/usuarios/$id',
          {'nomeCompleto': nomeCompleto, 'perfil': perfil, 'ativo': ativo, 'patiosIds': patiosIds}));

  Future<List<PatioAdmin>> patiosDaEmpresa() async => _lista(await _get('/api/admin/patios'), PatioAdmin.deJson);

  Future<PatioAdmin> criarPatio(String nome, int totalVagas, double valorHora) async => PatioAdmin.deJson(
      await _post('/api/admin/patios', {'nome': nome, 'totalVagas': totalVagas, 'valorHora': valorHora, 'minutosTolerancia': 0}));

  Future<PatioAdmin> atualizarPatio(String id, String nome, bool ativo) async =>
      PatioAdmin.deJson(await _enviar('PUT', '/api/admin/patios/$id', {'nome': nome, 'ativo': ativo}));

  // ---------- Mensalistas (pátio selecionado) ----------

  Future<List<Mensalista>> mensalistas({String? situacao, String? busca}) async {
    final parametros = {
      'situacao': ?situacao,
      if (busca != null && busca.trim().isNotEmpty) 'busca': busca.trim(),
    };
    final consulta = parametros.isEmpty ? '' : '?${Uri(queryParameters: parametros).query}';
    return _lista(await _get('/api/mensalistas$consulta'), Mensalista.deJson);
  }

  Future<Mensalista> salvarMensalista({
    String? id,
    required String nome,
    String? telefone,
    String? documento,
    String? observacoes,
    required List<String> placas,
    bool ativo = true,
    String? email,
  }) async {
    final corpo = {
      'nome': nome,
      'telefone': telefone,
      'documento': documento,
      'observacoes': observacoes,
      'placas': placas,
      'ativo': ativo,
      'email': email,
    };
    return Mensalista.deJson(
        id == null ? await _post('/api/mensalistas', corpo) : await _enviar('PUT', '/api/mensalistas/$id', corpo));
  }

  /// Novo convite de acesso ao app para o e-mail do cadastro
  Future<Mensalista> reenviarConvite(String id) async => Mensalista.deJson(await _post('/api/mensalistas/$id/convite', null));

  Future<List<PagamentoMensalidade>> pagamentosMensalista(String id) async =>
      _lista(await _get('/api/mensalistas/$id/pagamentos'), PagamentoMensalidade.deJson);

  Future<Mensalista> registrarPagamento(String id, {required int meses, double? valor, required String forma, String? observacao}) async =>
      Mensalista.deJson(await _post('/api/mensalistas/$id/pagamentos',
          {'meses': meses, 'valor': valor, 'formaPagamento': forma, 'observacao': observacao}));

  // ---------- Plataforma (superadmin) ----------

  // ---------- Relatórios ----------

  /// Relatório do dono (admin): todos os pátios da empresa, ou só [patioId]
  Future<RelatorioEmpresa> relatorioEmpresa(PeriodoRelatorio periodo, {String? patioId}) async {
    final consulta = Uri(queryParameters: {...periodo.paraConsulta(), 'patioId': ?patioId}).query;
    return RelatorioEmpresa.deJson(await _get('/api/relatorios/empresa?$consulta'));
  }

  Future<RelatorioPlataforma> relatorioPlataforma(PeriodoRelatorio periodo) async => RelatorioPlataforma.deJson(
      await _get('/api/plataforma/relatorio?${Uri(queryParameters: periodo.paraConsulta()).query}'));

  // ---------- Auditoria e logs do sistema ----------

  Future<OpcoesAuditoria> opcoesAuditoria({String? empresaId}) async => OpcoesAuditoria.deJson(
      await _get('/api/auditoria/opcoes${empresaId == null ? '' : '?${Uri(queryParameters: {'empresaId': empresaId}).query}'}'));

  Future<PaginaAuditoria> auditoria(FiltroAuditoria filtro, {int pagina = 1}) async => PaginaAuditoria.deJson(await _get(
      '/api/auditoria?${Uri(queryParameters: {...filtro.paraConsulta(), 'pagina': '$pagina'}).query}'));

  Future<PaginaLogsSistema> logsSistema(PeriodoRelatorio periodo, {String nivel = 'Information', String? texto, int pagina = 1}) async =>
      PaginaLogsSistema.deJson(await _get('/api/plataforma/logs?${Uri(queryParameters: {
            ...periodo.paraConsulta(),
            'nivel': nivel,
            if (texto != null && texto.trim().isNotEmpty) 'texto': texto.trim(),
            'pagina': '$pagina',
          }).query}'));

  // ---------- Planos e assinatura ----------

  Future<List<RecursoPlano>> recursosDePlano() async => _lista(await _get('/api/plataforma/recursos'), RecursoPlano.deJson);

  Future<List<Plano>> planos() async => _lista(await _get('/api/plataforma/planos'), Plano.deJson);

  Future<Plano> salvarPlano(Plano plano, {String? id}) async => Plano.deJson(id == null
      ? await _post('/api/plataforma/planos', plano.paraJson())
      : await _enviar('PUT', '/api/plataforma/planos/$id', plano.paraJson()));

  Future<AssinaturaEmpresa> assinaturaEmpresa(String empresaId) async =>
      AssinaturaEmpresa.deJson(await _get('/api/plataforma/empresas/$empresaId/assinatura'));

  Future<AssinaturaEmpresa> salvarAssinatura(String empresaId,
          {required String planoId, double? precoNegociado, required int diaVencimento, DateTime? testeAte, String? observacoes}) async =>
      AssinaturaEmpresa.deJson(await _enviar('PUT', '/api/plataforma/empresas/$empresaId/assinatura', {
        'planoId': planoId,
        'precoPorPatioNegociado': precoNegociado,
        'diaVencimento': diaVencimento,
        'testeAte': testeAte == null ? null : dataApi(testeAte),
        'observacoes': observacoes,
      }));

  /// Códigos dos recursos liberados pelo plano da empresa do usuário
  Future<Set<String>> recursosLiberados() async => ((await _get('/api/assinatura/recursos')) as List).cast<String>().toSet();

  Future<SituacaoCobranca> situacaoCobranca() async => SituacaoCobranca.deJson(await _get('/api/assinatura/situacao'));

  Future<MinhaAssinatura> minhaAssinatura() async => MinhaAssinatura.deJson(await _get('/api/admin/assinatura'));

  // ---------- Faturas do PkLots ----------

  Future<List<Fatura>> faturas({String? empresaId, String? situacao}) async {
    final consulta = Uri(queryParameters: {'empresaId': ?empresaId, 'situacao': ?situacao}).query;
    return _lista(await _get('/api/plataforma/faturas${consulta.isEmpty ? '' : '?$consulta'}'), Fatura.deJson);
  }

  Future<int> emitirFaturas() async => ((await _post('/api/plataforma/faturas/emitir', null)) as Map<String, dynamic>)['emitidas'] as int;

  Future<Fatura> lancarFatura(String empresaId, String descricao, double valor, DateTime vencimento) async => Fatura.deJson(
      await _post('/api/plataforma/empresas/$empresaId/faturas', {'descricao': descricao, 'valor': valor, 'vencimento': dataApi(vencimento)}));

  Future<Fatura> baixarFatura(String id, {required String forma, DateTime? pagaEm, double? valorPago, String? observacao}) async =>
      Fatura.deJson(await _post('/api/plataforma/faturas/$id/pagamento', {
        'formaPagamento': forma,
        'pagaEm': pagaEm == null ? null : dataApi(pagaEm),
        'valorPago': valorPago,
        'observacao': observacao,
      }));

  Future<Fatura> cancelarFatura(String id, String motivo) async =>
      Fatura.deJson(await _post('/api/plataforma/faturas/$id/cancelar', {'motivo': motivo}));

  Future<RecebimentoPlataforma> recebimentoPlataforma() async =>
      RecebimentoPlataforma.deJson(await _get('/api/plataforma/recebimento'));

  Future<RecebimentoPlataforma> salvarRecebimentoPlataforma(String? chave, String? nome, String? cidade) async =>
      RecebimentoPlataforma.deJson(await _enviar('PUT', '/api/plataforma/recebimento',
          {'chavePix': chave, 'nomeRecebedor': nome, 'cidadeRecebedor': cidade}));

  /// Faturas da empresa do admin logado (com o PIX das abertas)
  Future<List<Fatura>> minhasFaturas() async => _lista(await _get('/api/admin/faturas'), Fatura.deJson);

  Future<List<EmpresaPlataforma>> empresas() async => _lista(await _get('/api/plataforma/empresas'), EmpresaPlataforma.deJson);

  Future<EmpresaPlataforma> criarEmpresa({
    required String nome,
    String? documento,
    required String nomePatio,
    required int totalVagas,
    required String nomeAdmin,
    required String emailAdmin, // recebe o convite por e-mail para criar a senha
    String? planoId,
  }) async =>
      EmpresaPlataforma.deJson(await _post('/api/plataforma/empresas', {
        'nome': nome,
        'documento': documento,
        'nomePatio': nomePatio,
        'totalVagas': totalVagas,
        'nomeAdmin': nomeAdmin,
        'emailAdmin': emailAdmin,
        'planoId': planoId,
      }));

  Future<EmpresaPlataforma> atualizarEmpresa(String id, String nome, String? documento, bool ativa) async =>
      EmpresaPlataforma.deJson(
          await _enviar('PUT', '/api/plataforma/empresas/$id', {'nome': nome, 'documento': documento, 'ativa': ativa}));

  Future<EmpresaPlataforma> reenviarConviteEmpresa(String id) async =>
      EmpresaPlataforma.deJson(await _post('/api/plataforma/empresas/$id/convite', null));

  // ---------- Infraestrutura ----------

  Map<String, String> get _cabecalhos => {
        'Accept': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
        'X-Patio-Id': ?patioAtualId,
      };

  Uri _uri(String caminho) => Uri.parse('$_urlBase$caminho');

  Future<dynamic> _get(String caminho) =>
      _executar(() => _http.get(_uri(caminho), headers: _cabecalhos));

  Future<dynamic> _post(String caminho, Object? corpo) => _enviar('POST', caminho, corpo);

  Future<dynamic> _enviar(String metodo, String caminho, Object? corpo) => _executar(() {
        final requisicao = http.Request(metodo, _uri(caminho))
          ..headers.addAll(_cabecalhos)
          ..headers['Content-Type'] = 'application/json';
        if (corpo != null) requisicao.body = jsonEncode(corpo);
        return _http.send(requisicao).then(http.Response.fromStream);
      });

  Future<dynamic> _enviarImagem(String caminho, List<int> bytes, String nomeArquivo) => _executar(() {
        final requisicao = http.MultipartRequest('POST', _uri(caminho))
          ..headers.addAll(_cabecalhos)
          ..files.add(http.MultipartFile.fromBytes('imagem', bytes, filename: nomeArquivo));
        return _http.send(requisicao).then(http.Response.fromStream);
      });

  Future<dynamic> _executar(Future<http.Response> Function() chamada) async {
    final http.Response resposta;
    try {
      resposta = await chamada().timeout(_tempoLimite);
    } on TimeoutException {
      throw ExcecaoApi('O servidor demorou para responder. Tente novamente.');
    } catch (_) {
      throw ExcecaoApi('Não foi possível conectar ao servidor. Verifique sua conexão.');
    }

    if (resposta.statusCode == 204) return null;

    final corpo = resposta.body.isEmpty ? null : jsonDecode(utf8.decode(resposta.bodyBytes));

    if (resposta.statusCode >= 200 && resposta.statusCode < 300) return corpo;

    if (resposta.statusCode == 401 && token != null) aoExpirarSessao?.call();
    if (resposta.statusCode == 403 && corpo is Map<String, dynamic> && corpo['codigo'] == 'troca_senha') {
      aoExigirTrocaSenha?.call();
    }

    final mensagem = corpo is Map<String, dynamic>
        ? (corpo['detail'] ?? corpo['title'] ?? 'Erro inesperado.') as String
        : switch (resposta.statusCode) {
            401 => 'Sessão expirada. Faça login novamente.',
            403 => 'Você não tem permissão para esta ação.',
            429 => 'Muitas requisições. Aguarde alguns segundos.',
            _ => 'Erro inesperado (${resposta.statusCode}).',
          };
    throw ExcecaoApi(mensagem, status: resposta.statusCode);
  }

  List<T> _lista<T>(dynamic json, T Function(Map<String, dynamic>) conversor) =>
      (json as List).map((e) => conversor(e as Map<String, dynamic>)).toList();
}
