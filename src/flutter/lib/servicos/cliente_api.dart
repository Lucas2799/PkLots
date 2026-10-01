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

  /// Chamado quando a API responde 401 com um token presente (sessão expirada)
  void Function()? aoExpirarSessao;

  // ---------- Autenticação ----------

  Future<Token> login(String email, String senha) async =>
      Token.deJson(await _post('/api/auth/login', {'email': email, 'senha': senha}));

  Future<Token> registrar(String email, String senha, String nomeCompleto) async => Token.deJson(
      await _post('/api/auth/registrar', {'email': email, 'senha': senha, 'nomeCompleto': nomeCompleto}));

  // ---------- Vagas ----------

  Future<ResumoVagas> resumoVagas() async => ResumoVagas.deJson(await _get('/api/vagas/resumo'));

  Future<List<Vaga>> vagas() async => _lista(await _get('/api/vagas'), Vaga.deJson);

  Future<Vaga> marcarManutencao(String vagaId, String? observacoes) async =>
      Vaga.deJson(await _enviar('PUT', '/api/vagas/$vagaId/manutencao', {'observacoes': observacoes}));

  Future<Vaga> liberarManutencao(String vagaId) async =>
      Vaga.deJson(await _enviar('DELETE', '/api/vagas/$vagaId/manutencao', null));

  // ---------- Motorista ----------

  Future<List<Veiculo>> meusVeiculos() async => _lista(await _get('/api/motorista/veiculos'), Veiculo.deJson);

  Future<Veiculo> cadastrarVeiculo(String placa, {String? modelo, String? cor}) async => Veiculo.deJson(
      await _post('/api/motorista/veiculos', {'placa': placa, 'modelo': modelo, 'cor': cor}));

  Future<EstacionamentoAtual?> estacionamentoAtual() async {
    final json = await _get('/api/motorista/estacionamento-atual');
    return json == null ? null : EstacionamentoAtual.deJson(json);
  }

  Future<List<ItemHistorico>> meuHistorico() async =>
      _lista(await _get('/api/motorista/historico'), ItemHistorico.deJson);

  Future<Saida> iniciarSaida() async => Saida.deJson(await _post('/api/motorista/iniciar-saida', null));

  // ---------- Gestão ----------

  Future<Dashboard> dashboard() async => Dashboard.deJson(await _get('/api/dashboard'));

  Future<List<ItemHistorico>> historicoVeiculo(String placa) async =>
      _lista(await _get('/api/veiculos/${Uri.encodeComponent(placa)}/historico'), ItemHistorico.deJson);

  Future<Entrada> registrarEntrada(String placa) async =>
      Entrada.deJson(await _post('/api/entradas/', {'placa': placa, 'confianca': 1, 'metodoReconhecimento': 'manual'}));

  Future<Saida> registrarSaida(String placa) async => Saida.deJson(await _post('/api/saidas/', {'placa': placa}));

  /// Saída pela leitura do ticket: aceita "PKLOTS:K7M2Q9XD", "K7M2-Q9XD" etc.
  Future<Saida> registrarSaidaPorTicket(String codigo) async =>
      Saida.deJson(await _post('/api/saidas/ticket', {'codigo': codigo}));

  Future<Entrada> registrarEntradaPorImagem(List<int> bytes, String nomeArquivo) async =>
      Entrada.deJson(await _enviarImagem('/api/entradas/imagem', bytes, nomeArquivo));

  Future<Saida> registrarSaidaPorImagem(List<int> bytes, String nomeArquivo) async =>
      Saida.deJson(await _enviarImagem('/api/saidas/imagem', bytes, nomeArquivo));

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

  Future<ConfiguracaoPatio> configuracao() async => ConfiguracaoPatio.deJson(await _get('/api/admin/configuracao'));

  Future<ConfiguracaoPatio> salvarConfiguracao(String nome, double valorHora, int minutosTolerancia) async =>
      ConfiguracaoPatio.deJson(await _enviar('PUT', '/api/admin/configuracao',
          {'nome': nome, 'valorHora': valorHora, 'minutosTolerancia': minutosTolerancia}));

  Future<ConfiguracaoPatio> alterarQuantidadeVagas(int total) async =>
      ConfiguracaoPatio.deJson(await _enviar('PUT', '/api/admin/vagas/quantidade', {'total': total}));

  Future<List<UsuarioAdmin>> usuarios() async => _lista(await _get('/api/admin/usuarios'), UsuarioAdmin.deJson);

  Future<UsuarioAdmin> criarUsuario(String email, String nomeCompleto, String senha, String perfil) async =>
      UsuarioAdmin.deJson(await _post('/api/admin/usuarios',
          {'email': email, 'nomeCompleto': nomeCompleto, 'senha': senha, 'perfil': perfil}));

  Future<UsuarioAdmin> atualizarUsuario(String id, String nomeCompleto, String perfil, bool ativo) async =>
      UsuarioAdmin.deJson(await _enviar('PUT', '/api/admin/usuarios/$id',
          {'nomeCompleto': nomeCompleto, 'perfil': perfil, 'ativo': ativo}));

  Future<void> redefinirSenha(String id, String senha) =>
      _enviar('PUT', '/api/admin/usuarios/$id/senha', {'senha': senha});

  // ---------- Infraestrutura ----------

  Map<String, String> get _cabecalhos => {
        'Accept': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
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
