// Modelos das respostas da API PkLots (JSON em camelCase)

DateTime _data(dynamic valor) => DateTime.parse(valor as String);
DateTime? _dataOpcional(dynamic valor) => valor == null ? null : DateTime.parse(valor as String);
double _numero(dynamic valor) => (valor as num?)?.toDouble() ?? 0;

class Token {
  final String tokenAcesso;
  final int expiraEm;
  final String perfil;

  Token({required this.tokenAcesso, required this.expiraEm, required this.perfil});

  factory Token.deJson(Map<String, dynamic> json) => Token(
        tokenAcesso: json['tokenAcesso'] as String,
        expiraEm: json['expiraEm'] as int,
        perfil: json['perfil'] as String,
      );
}

class ResumoVagas {
  final int total;
  final int disponiveis;
  final int ocupadas;
  final int manutencao;
  final DateTime atualizadoEm;

  ResumoVagas({
    required this.total,
    required this.disponiveis,
    required this.ocupadas,
    required this.manutencao,
    required this.atualizadoEm,
  });

  double get percentualDisponivel => total == 0 ? 0 : disponiveis / total;

  factory ResumoVagas.deJson(Map<String, dynamic> json) => ResumoVagas(
        total: json['total'] as int,
        disponiveis: json['disponiveis'] as int,
        ocupadas: json['ocupadas'] as int,
        manutencao: json['manutencao'] as int,
        atualizadoEm: _data(json['atualizadoEm']),
      );
}

class Vaga {
  static const disponivel = 'disponivel';
  static const ocupada = 'ocupada';
  static const manutencao = 'manutencao';

  final String id;
  final String numero;
  final String fileira;
  final int indice;
  final String situacao;
  final String? placaAtual;
  final DateTime? ocupadaDesde;
  final String? observacoes;

  Vaga({
    required this.id,
    required this.numero,
    required this.fileira,
    required this.indice,
    required this.situacao,
    this.placaAtual,
    this.ocupadaDesde,
    this.observacoes,
  });

  factory Vaga.deJson(Map<String, dynamic> json) => Vaga(
        id: json['id'] as String,
        numero: json['numero'] as String,
        fileira: json['fileira'] as String,
        indice: json['indice'] as int,
        situacao: json['situacao'] as String,
        placaAtual: json['placaAtual'] as String?,
        ocupadaDesde: _dataOpcional(json['ocupadaDesde']),
        observacoes: json['observacoes'] as String?,
      );
}

class Veiculo {
  final String id;
  final String placa;
  final String? modelo;
  final String? cor;

  Veiculo({required this.id, required this.placa, this.modelo, this.cor});

  factory Veiculo.deJson(Map<String, dynamic> json) => Veiculo(
        id: json['id'] as String,
        placa: json['placa'] as String,
        modelo: json['modelo'] as String?,
        cor: json['cor'] as String?,
      );
}

class EstacionamentoAtual {
  final String entradaId;
  final String placa;
  final DateTime dataHoraEntrada;
  final String duracao;
  final String? numeroVaga;
  final double valorEstimado;

  EstacionamentoAtual({
    required this.entradaId,
    required this.placa,
    required this.dataHoraEntrada,
    required this.duracao,
    this.numeroVaga,
    required this.valorEstimado,
  });

  factory EstacionamentoAtual.deJson(Map<String, dynamic> json) => EstacionamentoAtual(
        entradaId: json['entradaId'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        duracao: json['duracao'] as String,
        numeroVaga: json['numeroVaga'] as String?,
        valorEstimado: _numero(json['valorEstimado']),
      );
}

class ItemHistorico {
  final String entradaId;
  final String placa;
  final DateTime dataHoraEntrada;
  final DateTime? dataHoraSaida;
  final String? numeroVaga;
  final String? duracaoFormatada;
  final double? valorTotal;
  final String situacao;

  ItemHistorico({
    required this.entradaId,
    required this.placa,
    required this.dataHoraEntrada,
    this.dataHoraSaida,
    this.numeroVaga,
    this.duracaoFormatada,
    this.valorTotal,
    required this.situacao,
  });

  bool get emAndamento => dataHoraSaida == null;

  factory ItemHistorico.deJson(Map<String, dynamic> json) => ItemHistorico(
        entradaId: json['entradaId'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        dataHoraSaida: _dataOpcional(json['dataHoraSaida']),
        numeroVaga: json['numeroVaga'] as String?,
        duracaoFormatada: json['duracaoFormatada'] as String?,
        valorTotal: (json['valorTotal'] as num?)?.toDouble(),
        situacao: json['situacao'] as String,
      );
}

class Entrada {
  final String id;
  final String placa;
  final DateTime dataHoraEntrada;
  final String? numeroVaga;
  final double confianca;

  /// Resultado da impressão automática: impresso, falhou, navegador ou null
  final String? impressao;

  Entrada({
    required this.id,
    required this.placa,
    required this.dataHoraEntrada,
    this.numeroVaga,
    required this.confianca,
    this.impressao,
  });

  factory Entrada.deJson(Map<String, dynamic> json) => Entrada(
        id: json['id'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        numeroVaga: json['numeroVaga'] as String?,
        confianca: _numero(json['confianca']),
        impressao: json['impressao'] as String?,
      );
}

class Saida {
  final String id;
  final String placa;
  final DateTime dataHoraEntrada;
  final DateTime dataHoraSaida;
  final String duracaoFormatada;
  final double valorHora;
  final double valorTotal;
  final String situacaoPagamento;
  final String? numeroVaga;
  final String nomePatio;

  /// Saída dentro da tolerância gratuita
  bool get isento => situacaoPagamento == 'isento';

  Saida({
    required this.id,
    required this.placa,
    required this.dataHoraEntrada,
    required this.dataHoraSaida,
    required this.duracaoFormatada,
    required this.valorHora,
    required this.valorTotal,
    this.situacaoPagamento = 'calculado',
    this.numeroVaga,
    this.nomePatio = '',
  });

  factory Saida.deJson(Map<String, dynamic> json) => Saida(
        id: json['id'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        dataHoraSaida: _data(json['dataHoraSaida']),
        duracaoFormatada: json['duracaoFormatada'] as String,
        valorHora: _numero(json['valorHora']),
        valorTotal: _numero(json['valorTotal']),
        situacaoPagamento: (json['situacaoPagamento'] as String?) ?? 'calculado',
        numeroVaga: json['numeroVaga'] as String?,
        nomePatio: (json['nomePatio'] as String?) ?? '',
      );
}

class ResumoDia {
  final int entradas;
  final int saidas;
  final double receita;
  final String mediaEstadia;

  ResumoDia({required this.entradas, required this.saidas, required this.receita, required this.mediaEstadia});

  factory ResumoDia.deJson(Map<String, dynamic> json) => ResumoDia(
        entradas: json['entradas'] as int,
        saidas: json['saidas'] as int,
        receita: _numero(json['receita']),
        mediaEstadia: json['mediaEstadia'] as String,
      );
}

class OcupacaoHora {
  final DateTime hora;
  final int ocupadas;
  final double percentual;

  OcupacaoHora({required this.hora, required this.ocupadas, required this.percentual});

  factory OcupacaoHora.deJson(Map<String, dynamic> json) => OcupacaoHora(
        hora: _data(json['hora']),
        ocupadas: json['ocupadas'] as int,
        percentual: _numero(json['percentual']),
      );
}

class VeiculoEstacionado {
  final String entradaId;
  final String placa;
  final DateTime dataHoraEntrada;
  final String duracao;
  final String? numeroVaga;

  VeiculoEstacionado({required this.entradaId, required this.placa, required this.dataHoraEntrada, required this.duracao, this.numeroVaga});

  factory VeiculoEstacionado.deJson(Map<String, dynamic> json) => VeiculoEstacionado(
        entradaId: json['entradaId'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        duracao: json['duracao'] as String,
        numeroVaga: json['numeroVaga'] as String?,
      );
}

class Dashboard {
  final String nomePatio;
  final ResumoDia hoje;
  final ResumoVagas vagas;
  final List<OcupacaoHora> ocupacao24h;
  final List<VeiculoEstacionado> estacionados;

  Dashboard({
    required this.nomePatio,
    required this.hoje,
    required this.vagas,
    required this.ocupacao24h,
    required this.estacionados,
  });

  factory Dashboard.deJson(Map<String, dynamic> json) => Dashboard(
        nomePatio: json['nomePatio'] as String,
        hoje: ResumoDia.deJson(json['hoje'] as Map<String, dynamic>),
        vagas: ResumoVagas.deJson(json['vagas'] as Map<String, dynamic>),
        ocupacao24h: (json['ocupacao24h'] as List).map((e) => OcupacaoHora.deJson(e as Map<String, dynamic>)).toList(),
        estacionados:
            (json['estacionados'] as List).map((e) => VeiculoEstacionado.deJson(e as Map<String, dynamic>)).toList(),
      );
}

class ConfiguracaoPatio {
  final String nome;
  final double valorHora;
  final int minutosTolerancia;
  final int totalVagas;
  final int vagasPorFileira;
  final int maximoVagas;

  ConfiguracaoPatio({
    required this.nome,
    required this.valorHora,
    required this.minutosTolerancia,
    required this.totalVagas,
    required this.vagasPorFileira,
    required this.maximoVagas,
  });

  factory ConfiguracaoPatio.deJson(Map<String, dynamic> json) => ConfiguracaoPatio(
        nome: json['nome'] as String,
        valorHora: _numero(json['valorHora']),
        minutosTolerancia: json['minutosTolerancia'] as int,
        totalVagas: json['totalVagas'] as int,
        vagasPorFileira: json['vagasPorFileira'] as int,
        maximoVagas: json['maximoVagas'] as int,
      );
}

class UsuarioAdmin {
  static const perfis = ['admin', 'operador', 'motorista'];

  final String id;
  final String email;
  final String nomeCompleto;
  final String perfil;
  final bool ativo;
  final DateTime? ultimoLogin;

  UsuarioAdmin({
    required this.id,
    required this.email,
    required this.nomeCompleto,
    required this.perfil,
    required this.ativo,
    this.ultimoLogin,
  });

  factory UsuarioAdmin.deJson(Map<String, dynamic> json) => UsuarioAdmin(
        id: json['id'] as String,
        email: json['email'] as String,
        nomeCompleto: json['nomeCompleto'] as String,
        perfil: json['perfil'] as String,
        ativo: json['ativo'] as bool,
        ultimoLogin: _dataOpcional(json['ultimoLogin']),
      );
}

class TicketEntrada {
  final String entradaId;
  final String codigo;
  final String nomePatio;
  final String placa;
  final String? numeroVaga;
  final DateTime dataHoraEntrada;
  final double valorHora;
  final int minutosTolerancia;
  final int larguraPapelMm;
  final String conteudoQrCode;

  TicketEntrada({
    required this.entradaId,
    required this.codigo,
    required this.nomePatio,
    required this.placa,
    this.numeroVaga,
    required this.dataHoraEntrada,
    required this.valorHora,
    required this.minutosTolerancia,
    required this.larguraPapelMm,
    required this.conteudoQrCode,
  });

  factory TicketEntrada.deJson(Map<String, dynamic> json) => TicketEntrada(
        entradaId: json['entradaId'] as String,
        codigo: json['codigo'] as String,
        nomePatio: json['nomePatio'] as String,
        placa: json['placa'] as String,
        numeroVaga: json['numeroVaga'] as String?,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        valorHora: _numero(json['valorHora']),
        minutosTolerancia: json['minutosTolerancia'] as int,
        larguraPapelMm: json['larguraPapelMm'] as int,
        conteudoQrCode: json['conteudoQrCode'] as String,
      );
}

class ConfiguracaoImpressao {
  static const desativada = 'desativada';
  static const rede = 'rede';
  static const navegador = 'navegador';

  final String modo;
  final String? impressoraHost;
  final int impressoraPorta;
  final int larguraPapelMm;
  final bool imprimirEntradaAutomaticamente;

  ConfiguracaoImpressao({
    required this.modo,
    this.impressoraHost,
    this.impressoraPorta = 9100,
    this.larguraPapelMm = 80,
    this.imprimirEntradaAutomaticamente = true,
  });

  factory ConfiguracaoImpressao.deJson(Map<String, dynamic> json) => ConfiguracaoImpressao(
        modo: json['modo'] as String,
        impressoraHost: json['impressoraHost'] as String?,
        impressoraPorta: json['impressoraPorta'] as int,
        larguraPapelMm: json['larguraPapelMm'] as int,
        imprimirEntradaAutomaticamente: json['imprimirEntradaAutomaticamente'] as bool,
      );

  Map<String, dynamic> paraJson() => {
        'modo': modo,
        'impressoraHost': impressoraHost,
        'impressoraPorta': impressoraPorta,
        'larguraPapelMm': larguraPapelMm,
        'imprimirEntradaAutomaticamente': imprimirEntradaAutomaticamente,
      };
}
