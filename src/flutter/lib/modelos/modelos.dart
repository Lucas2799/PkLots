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

  /// Vagas reservadas a mensalistas ainda não usadas (não servem a avulsos)
  final int reservadasLivres;
  int get disponiveisAvulsos => (disponiveis - reservadasLivres).clamp(0, disponiveis);

  ResumoVagas({
    required this.total,
    required this.disponiveis,
    required this.ocupadas,
    required this.manutencao,
    required this.atualizadoEm,
    this.reservadasLivres = 0,
  });

  double get percentualDisponivel => total == 0 ? 0 : disponiveis / total;

  factory ResumoVagas.deJson(Map<String, dynamic> json) => ResumoVagas(
        total: json['total'] as int,
        disponiveis: json['disponiveis'] as int,
        ocupadas: json['ocupadas'] as int,
        manutencao: json['manutencao'] as int,
        atualizadoEm: _data(json['atualizadoEm']),
        reservadasLivres: (json['reservadasLivres'] as int?) ?? 0,
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
  final String nomePatio;

  EstacionamentoAtual({
    required this.entradaId,
    required this.placa,
    required this.dataHoraEntrada,
    required this.duracao,
    this.numeroVaga,
    required this.valorEstimado,
    this.nomePatio = '',
  });

  factory EstacionamentoAtual.deJson(Map<String, dynamic> json) => EstacionamentoAtual(
        entradaId: json['entradaId'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        duracao: json['duracao'] as String,
        numeroVaga: json['numeroVaga'] as String?,
        valorEstimado: _numero(json['valorEstimado']),
        nomePatio: (json['nomePatio'] as String?) ?? '',
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
  final String? nomePatio;

  ItemHistorico({
    required this.entradaId,
    required this.placa,
    required this.dataHoraEntrada,
    this.dataHoraSaida,
    this.numeroVaga,
    this.duracaoFormatada,
    this.valorTotal,
    required this.situacao,
    this.nomePatio,
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
        nomePatio: json['nomePatio'] as String?,
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

  /// Nome e situação (em_dia, vencendo, vencido) quando a placa é de um mensalista
  final String? mensalista;
  final String? situacaoMensalista;
  final DateTime? mensalidadeValidaAte;

  Entrada({
    required this.id,
    required this.placa,
    required this.dataHoraEntrada,
    this.numeroVaga,
    required this.confianca,
    this.impressao,
    this.mensalista,
    this.situacaoMensalista,
    this.mensalidadeValidaAte,
  });

  factory Entrada.deJson(Map<String, dynamic> json) => Entrada(
        id: json['id'] as String,
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        numeroVaga: json['numeroVaga'] as String?,
        confianca: _numero(json['confianca']),
        impressao: json['impressao'] as String?,
        mensalista: json['mensalista'] as String?,
        situacaoMensalista: json['situacaoMensalista'] as String?,
        mensalidadeValidaAte: _dataOpcional(json['mensalidadeValidaAte']),
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
  final String? mensalista;

  /// Aviso para o operador (ex.: mensalidade vencida, cobrado como avulso)
  final String? aviso;

  bool get mensalistaSemCobranca => situacaoPagamento == 'mensalista';

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
    this.mensalista,
    this.aviso,
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
        mensalista: json['mensalista'] as String?,
        aviso: json['aviso'] as String?,
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
  final ResumoMensalistas? mensalistas;

  Dashboard({
    required this.nomePatio,
    required this.hoje,
    required this.vagas,
    required this.ocupacao24h,
    required this.estacionados,
    this.mensalistas,
  });

  factory Dashboard.deJson(Map<String, dynamic> json) => Dashboard(
        nomePatio: json['nomePatio'] as String,
        hoje: ResumoDia.deJson(json['hoje'] as Map<String, dynamic>),
        vagas: ResumoVagas.deJson(json['vagas'] as Map<String, dynamic>),
        ocupacao24h: (json['ocupacao24h'] as List).map((e) => OcupacaoHora.deJson(e as Map<String, dynamic>)).toList(),
        estacionados:
            (json['estacionados'] as List).map((e) => VeiculoEstacionado.deJson(e as Map<String, dynamic>)).toList(),
        mensalistas: json['mensalistas'] == null
            ? null
            : ResumoMensalistas.deJson(json['mensalistas'] as Map<String, dynamic>),
      );
}

class ConfiguracaoPatio {
  final String nome;
  final double valorHora;
  final int minutosTolerancia;
  final int totalVagas;
  final int vagasPorFileira;
  final int maximoVagas;
  final double valorMensalidade;
  final int vagasReservadasMensalistas;

  ConfiguracaoPatio({
    required this.nome,
    required this.valorHora,
    required this.minutosTolerancia,
    required this.totalVagas,
    required this.vagasPorFileira,
    required this.maximoVagas,
    this.valorMensalidade = 0,
    this.vagasReservadasMensalistas = 0,
  });

  factory ConfiguracaoPatio.deJson(Map<String, dynamic> json) => ConfiguracaoPatio(
        nome: json['nome'] as String,
        valorHora: _numero(json['valorHora']),
        minutosTolerancia: json['minutosTolerancia'] as int,
        totalVagas: json['totalVagas'] as int,
        vagasPorFileira: json['vagasPorFileira'] as int,
        maximoVagas: json['maximoVagas'] as int,
        valorMensalidade: _numero(json['valorMensalidade']),
        vagasReservadasMensalistas: (json['vagasReservadasMensalistas'] as int?) ?? 0,
      );
}

class UsuarioAdmin {
  static const perfis = ['admin', 'operador'];

  final String id;
  final String email;
  final String nomeCompleto;
  final String perfil;
  final bool ativo;
  final DateTime? ultimoLogin;
  final List<String> patiosIds;

  UsuarioAdmin({
    required this.id,
    required this.email,
    required this.nomeCompleto,
    required this.perfil,
    required this.ativo,
    this.ultimoLogin,
    this.patiosIds = const [],
  });

  factory UsuarioAdmin.deJson(Map<String, dynamic> json) => UsuarioAdmin(
        id: json['id'] as String,
        email: json['email'] as String,
        nomeCompleto: json['nomeCompleto'] as String,
        perfil: json['perfil'] as String,
        ativo: json['ativo'] as bool,
        ultimoLogin: _dataOpcional(json['ultimoLogin']),
        patiosIds: ((json['patiosIds'] as List?) ?? []).cast<String>(),
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

/// Pátio do seletor do painel
class PatioResumo {
  final String id;
  final String nome;

  PatioResumo({required this.id, required this.nome});

  factory PatioResumo.deJson(Map<String, dynamic> json) => PatioResumo(id: json['id'] as String, nome: json['nome'] as String);
}

/// Pátio na administração da empresa
class PatioAdmin {
  final String id;
  final String nome;
  final bool ativo;
  final int totalVagas;
  final double valorHora;
  final int veiculosEstacionados;

  PatioAdmin({
    required this.id,
    required this.nome,
    required this.ativo,
    required this.totalVagas,
    required this.valorHora,
    required this.veiculosEstacionados,
  });

  factory PatioAdmin.deJson(Map<String, dynamic> json) => PatioAdmin(
        id: json['id'] as String,
        nome: json['nome'] as String,
        ativo: json['ativo'] as bool,
        totalVagas: json['totalVagas'] as int,
        valorHora: _numero(json['valorHora']),
        veiculosEstacionados: json['veiculosEstacionados'] as int,
      );
}

/// Empresa cliente (área do superadmin)
class EmpresaPlataforma {
  final String id;
  final String nome;
  final String? documento;
  final bool ativa;
  final int patios;
  final int usuarios;
  final DateTime criadoEm;

  EmpresaPlataforma({
    required this.id,
    required this.nome,
    this.documento,
    required this.ativa,
    required this.patios,
    required this.usuarios,
    required this.criadoEm,
  });

  factory EmpresaPlataforma.deJson(Map<String, dynamic> json) => EmpresaPlataforma(
        id: json['id'] as String,
        nome: json['nome'] as String,
        documento: json['documento'] as String?,
        ativa: json['ativa'] as bool,
        patios: json['patios'] as int,
        usuarios: json['usuarios'] as int,
        criadoEm: _data(json['criadoEm']),
      );
}

/// Pátio visível ao motorista, com vagas livres agora
class PatioDisponibilidade {
  final String id;
  final String nome;
  final String nomeEmpresa;
  final int disponiveis;
  final int total;

  PatioDisponibilidade({
    required this.id,
    required this.nome,
    required this.nomeEmpresa,
    required this.disponiveis,
    required this.total,
  });

  factory PatioDisponibilidade.deJson(Map<String, dynamic> json) => PatioDisponibilidade(
        id: json['id'] as String,
        nome: json['nome'] as String,
        nomeEmpresa: json['nomeEmpresa'] as String,
        disponiveis: json['disponiveis'] as int,
        total: json['total'] as int,
      );
}

class ResumoMensalistas {
  final int ativos;
  final int emDia;
  final int vencendo;
  final int vencidos;
  final int estacionados;
  final int vagasReservadas;
  final int reservadasLivres;
  final double valorMensalidade;

  ResumoMensalistas({
    required this.ativos,
    required this.emDia,
    required this.vencendo,
    required this.vencidos,
    required this.estacionados,
    required this.vagasReservadas,
    required this.reservadasLivres,
    this.valorMensalidade = 0,
  });

  factory ResumoMensalistas.deJson(Map<String, dynamic> json) => ResumoMensalistas(
        ativos: json['ativos'] as int,
        emDia: json['emDia'] as int,
        vencendo: json['vencendo'] as int,
        vencidos: json['vencidos'] as int,
        estacionados: json['estacionados'] as int,
        vagasReservadas: json['vagasReservadas'] as int,
        reservadasLivres: json['reservadasLivres'] as int,
        valorMensalidade: _numero(json['valorMensalidade']),
      );
}

class Mensalista {
  static const emDia = 'em_dia';
  static const vencendo = 'vencendo';
  static const vencido = 'vencido';
  static const inativo = 'inativo';

  final String id;
  final String nome;
  final String? telefone;
  final String? documento;
  final String? observacoes;
  final List<String> placas;
  final DateTime? validoAte;
  final String situacao;
  final int? diasParaVencer;
  final bool ativo;
  final bool estacionado;

  Mensalista({
    required this.id,
    required this.nome,
    this.telefone,
    this.documento,
    this.observacoes,
    required this.placas,
    this.validoAte,
    required this.situacao,
    this.diasParaVencer,
    required this.ativo,
    required this.estacionado,
  });

  factory Mensalista.deJson(Map<String, dynamic> json) => Mensalista(
        id: json['id'] as String,
        nome: json['nome'] as String,
        telefone: json['telefone'] as String?,
        documento: json['documento'] as String?,
        observacoes: json['observacoes'] as String?,
        placas: ((json['placas'] as List?) ?? []).cast<String>(),
        validoAte: _dataOpcional(json['validoAte']),
        situacao: json['situacao'] as String,
        diasParaVencer: json['diasParaVencer'] as int?,
        ativo: json['ativo'] as bool,
        estacionado: (json['estacionado'] as bool?) ?? false,
      );
}

class PagamentoMensalidade {
  static const formas = {'dinheiro': 'Dinheiro', 'pix': 'PIX', 'cartao': 'Cartão', 'outro': 'Outro'};

  final String id;
  final double valor;
  final int meses;
  final DateTime validoDe;
  final DateTime validoAte;
  final String formaPagamento;
  final String? observacao;
  final DateTime pagoEm;

  PagamentoMensalidade({
    required this.id,
    required this.valor,
    required this.meses,
    required this.validoDe,
    required this.validoAte,
    required this.formaPagamento,
    this.observacao,
    required this.pagoEm,
  });

  factory PagamentoMensalidade.deJson(Map<String, dynamic> json) => PagamentoMensalidade(
        id: json['id'] as String,
        valor: _numero(json['valor']),
        meses: json['meses'] as int,
        validoDe: _data(json['validoDe']),
        validoAte: _data(json['validoAte']),
        formaPagamento: json['formaPagamento'] as String,
        observacao: json['observacao'] as String?,
        pagoEm: _data(json['pagoEm']),
      );
}
