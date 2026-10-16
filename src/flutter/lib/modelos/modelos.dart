// Modelos das respostas da API PkLots (JSON em camelCase)

DateTime _data(dynamic valor) => DateTime.parse(valor as String);
DateTime? _dataOpcional(dynamic valor) => valor == null ? null : DateTime.parse(valor as String);
double _numero(dynamic valor) => (valor as num?)?.toDouble() ?? 0;

class Token {
  final String tokenAcesso;
  final int expiraEm;
  final String perfil;

  /// Senha temporária ou pública: o app só permite trocar a senha
  final bool deveTrocarSenha;

  Token({required this.tokenAcesso, required this.expiraEm, required this.perfil, this.deveTrocarSenha = false});

  factory Token.deJson(Map<String, dynamic> json) => Token(
        tokenAcesso: json['tokenAcesso'] as String,
        expiraEm: json['expiraEm'] as int,
        perfil: json['perfil'] as String,
        deveTrocarSenha: (json['deveTrocarSenha'] as bool?) ?? false,
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

/// Contrato de mensalista visto no app (um por pátio em que a pessoa aceitou o convite)
class ContratoMensalista {
  final String id;
  final String nomePatio;
  final String nomeEmpresa;
  final List<String> placas;
  final DateTime? validoAte;
  final String situacao;
  final int? diasParaVencer;
  final List<EstadiaAtual> estacionados;

  ContratoMensalista({
    required this.id,
    required this.nomePatio,
    required this.nomeEmpresa,
    required this.placas,
    this.validoAte,
    required this.situacao,
    this.diasParaVencer,
    required this.estacionados,
  });

  factory ContratoMensalista.deJson(Map<String, dynamic> json) => ContratoMensalista(
        id: json['id'] as String,
        nomePatio: json['nomePatio'] as String,
        nomeEmpresa: json['nomeEmpresa'] as String,
        placas: ((json['placas'] as List?) ?? []).cast<String>(),
        validoAte: _dataOpcional(json['validoAte']),
        situacao: json['situacao'] as String,
        diasParaVencer: json['diasParaVencer'] as int?,
        estacionados: ((json['estacionados'] as List?) ?? [])
            .map((e) => EstadiaAtual.deJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Veículo do contrato estacionado agora (valor zero enquanto coberto pela mensalidade)
class EstadiaAtual {
  final String placa;
  final DateTime dataHoraEntrada;
  final String duracao;
  final String? numeroVaga;
  final double valorEstimado;

  EstadiaAtual({
    required this.placa,
    required this.dataHoraEntrada,
    required this.duracao,
    this.numeroVaga,
    required this.valorEstimado,
  });

  factory EstadiaAtual.deJson(Map<String, dynamic> json) => EstadiaAtual(
        placa: json['placa'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        duracao: json['duracao'] as String,
        numeroVaga: json['numeroVaga'] as String?,
        valorEstimado: _numero(json['valorEstimado']),
      );
}

/// Convite de mensalista aberto pelo link do e-mail (antes de aceitar)
class ConviteMensalista {
  final String nomeMensalista;
  final String nomePatio;
  final String email;
  final List<String> placasMascaradas;
  final bool contaExistente;

  ConviteMensalista({
    required this.nomeMensalista,
    required this.nomePatio,
    required this.email,
    required this.placasMascaradas,
    required this.contaExistente,
  });

  factory ConviteMensalista.deJson(Map<String, dynamic> json) => ConviteMensalista(
        nomeMensalista: json['nomeMensalista'] as String,
        nomePatio: json['nomePatio'] as String,
        email: json['email'] as String,
        placasMascaradas: ((json['placasMascaradas'] as List?) ?? []).cast<String>(),
        contaExistente: json['contaExistente'] as bool,
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

  bool get emAndamento => situacao == 'ativa';
  bool get cancelada => situacao == 'cancelada';

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

  /// Pagamento na saída: forma, dinheiro recebido e troco
  final String? formaPagamento;
  final double? valorRecebido;
  final double? troco;

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
    this.formaPagamento,
    this.valorRecebido,
    this.troco,
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
        formaPagamento: json['formaPagamento'] as String?,
        valorRecebido: (json['valorRecebido'] as num?)?.toDouble(),
        troco: (json['troco'] as num?)?.toDouble(),
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

  /// Convidado por e-mail e ainda sem senha criada
  final bool convitePendente;

  UsuarioAdmin({
    required this.id,
    required this.email,
    required this.nomeCompleto,
    required this.perfil,
    required this.ativo,
    this.ultimoLogin,
    this.patiosIds = const [],
    this.convitePendente = false,
  });

  factory UsuarioAdmin.deJson(Map<String, dynamic> json) => UsuarioAdmin(
        id: json['id'] as String,
        email: json['email'] as String,
        nomeCompleto: json['nomeCompleto'] as String,
        perfil: json['perfil'] as String,
        ativo: json['ativo'] as bool,
        ultimoLogin: _dataOpcional(json['ultimoLogin']),
        patiosIds: ((json['patiosIds'] as List?) ?? []).cast<String>(),
        convitePendente: (json['convitePendente'] as bool?) ?? false,
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

  /// Tabela de preço do pátio em texto (partes separadas por " · ")
  final String? descricaoPreco;

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
    this.descricaoPreco,
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
        descricaoPreco: json['descricaoPreco'] as String?,
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

  /// Usuários convidados que ainda não criaram a senha
  final int convitesPendentes;
  final String? nomePlano;
  final String situacaoCobranca;

  EmpresaPlataforma({
    required this.id,
    required this.nome,
    this.documento,
    required this.ativa,
    required this.patios,
    required this.usuarios,
    required this.criadoEm,
    this.convitesPendentes = 0,
    this.nomePlano,
    this.situacaoCobranca = 'em_dia',
  });

  factory EmpresaPlataforma.deJson(Map<String, dynamic> json) => EmpresaPlataforma(
        id: json['id'] as String,
        nome: json['nome'] as String,
        documento: json['documento'] as String?,
        ativa: json['ativa'] as bool,
        patios: json['patios'] as int,
        usuarios: json['usuarios'] as int,
        criadoEm: _data(json['criadoEm']),
        convitesPendentes: (json['convitesPendentes'] as int?) ?? 0,
        nomePlano: json['nomePlano'] as String?,
        situacaoCobranca: (json['situacaoCobranca'] as String?) ?? 'em_dia',
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

  /// Acesso ao app: e-mail do convite e situação (sem_email, convite_pendente, ativo)
  final String? email;
  final String acessoApp;

  static const semEmail = 'sem_email';
  static const convitePendente = 'convite_pendente';
  static const acessoAtivo = 'ativo';

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
    this.email,
    this.acessoApp = semEmail,
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
        email: json['email'] as String?,
        acessoApp: (json['acessoApp'] as String?) ?? semEmail,
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

/// Rótulos das formas de pagamento
const rotulosFormaPagamento = {'dinheiro': 'Dinheiro', 'pix': 'PIX', 'cartao': 'Cartão', 'outro': 'Outro'};

/// Cobrança da saída: valor congelado até [expiraEm]; sem confirmação o carro não sai
class CobrancaSaida {
  final String id;
  final String entradaId;
  final String placa;
  final String? numeroVaga;
  final String nomePatio;
  final DateTime dataHoraEntrada;
  final DateTime calculadoEm;
  final DateTime expiraEm;
  final String duracaoFormatada;
  final double valorHora;
  final double valor;
  final String situacaoPagamento;
  final bool requerPagamento;
  final List<String> formasAceitas;
  final String? pixCopiaECola;
  final String? mensalista;
  final String? aviso;

  /// Como o valor foi calculado (ex.: "1ª hora R$ 10,00 + 2 × 30 min de R$ 3,00")
  final String? detalhamento;

  CobrancaSaida({
    required this.id,
    required this.entradaId,
    required this.placa,
    this.numeroVaga,
    required this.nomePatio,
    required this.dataHoraEntrada,
    required this.calculadoEm,
    required this.expiraEm,
    required this.duracaoFormatada,
    required this.valorHora,
    required this.valor,
    required this.situacaoPagamento,
    required this.requerPagamento,
    required this.formasAceitas,
    this.pixCopiaECola,
    this.mensalista,
    this.aviso,
    this.detalhamento,
  });

  factory CobrancaSaida.deJson(Map<String, dynamic> json) => CobrancaSaida(
        id: json['id'] as String,
        entradaId: json['entradaId'] as String,
        placa: json['placa'] as String,
        numeroVaga: json['numeroVaga'] as String?,
        nomePatio: json['nomePatio'] as String,
        dataHoraEntrada: _data(json['dataHoraEntrada']),
        calculadoEm: _data(json['calculadoEm']),
        expiraEm: _data(json['expiraEm']),
        duracaoFormatada: json['duracaoFormatada'] as String,
        valorHora: _numero(json['valorHora']),
        valor: _numero(json['valor']),
        situacaoPagamento: json['situacaoPagamento'] as String,
        requerPagamento: json['requerPagamento'] as bool,
        formasAceitas: ((json['formasAceitas'] as List?) ?? []).cast<String>(),
        pixCopiaECola: json['pixCopiaECola'] as String?,
        mensalista: json['mensalista'] as String?,
        aviso: json['aviso'] as String?,
        detalhamento: json['detalhamento'] as String?,
      );
}

class ConfiguracaoPagamento {
  final String provedor;
  final String? chavePix;
  final String? nomeRecebedor;
  final String? cidadeRecebedor;
  final bool aceitaDinheiro;
  final bool aceitaPix;
  final bool aceitaCartao;
  final List<String> provedoresDisponiveis;

  ConfiguracaoPagamento({
    this.provedor = 'pix_manual',
    this.chavePix,
    this.nomeRecebedor,
    this.cidadeRecebedor,
    this.aceitaDinheiro = true,
    this.aceitaPix = true,
    this.aceitaCartao = true,
    this.provedoresDisponiveis = const ['pix_manual'],
  });

  factory ConfiguracaoPagamento.deJson(Map<String, dynamic> json) => ConfiguracaoPagamento(
        provedor: json['provedor'] as String,
        chavePix: json['chavePix'] as String?,
        nomeRecebedor: json['nomeRecebedor'] as String?,
        cidadeRecebedor: json['cidadeRecebedor'] as String?,
        aceitaDinheiro: json['aceitaDinheiro'] as bool,
        aceitaPix: json['aceitaPix'] as bool,
        aceitaCartao: json['aceitaCartao'] as bool,
        provedoresDisponiveis: ((json['provedoresDisponiveis'] as List?) ?? ['pix_manual']).cast<String>(),
      );

  Map<String, dynamic> paraJson() => {
        'provedor': provedor,
        'chavePix': chavePix,
        'nomeRecebedor': nomeRecebedor,
        'cidadeRecebedor': cidadeRecebedor,
        'aceitaDinheiro': aceitaDinheiro,
        'aceitaPix': aceitaPix,
        'aceitaCartao': aceitaCartao,
      };
}

class RecebimentoPorForma {
  final String forma;
  final int quantidade;
  final double total;

  RecebimentoPorForma({required this.forma, required this.quantidade, required this.total});

  factory RecebimentoPorForma.deJson(Map<String, dynamic> json) => RecebimentoPorForma(
      forma: json['forma'] as String, quantidade: json['quantidade'] as int, total: _numero(json['total']));
}

class MovimentoCaixa {
  final String tipo;
  final double valor;
  final String? motivo;
  final DateTime criadoEm;

  MovimentoCaixa({required this.tipo, required this.valor, this.motivo, required this.criadoEm});

  factory MovimentoCaixa.deJson(Map<String, dynamic> json) => MovimentoCaixa(
        tipo: json['tipo'] as String,
        valor: _numero(json['valor']),
        motivo: json['motivo'] as String?,
        criadoEm: _data(json['criadoEm']),
      );
}

class Caixa {
  final String id;
  final String patioId;
  final String nomePatio;
  final String operadorId;
  final String nomeOperador;
  final DateTime abertoEm;
  final DateTime? fechadoEm;
  final double trocoInicial;
  final List<RecebimentoPorForma> recebimentos;
  final int saidas;
  final double totalRecebido;
  final double suprimentos;
  final double sangrias;
  final double dinheiroEsperado;
  final double? dinheiroContado;
  final double? diferenca;
  final String? observacaoFechamento;
  final List<MovimentoCaixa> movimentos;

  bool get aberto => fechadoEm == null;

  Caixa({
    required this.id,
    required this.patioId,
    required this.nomePatio,
    required this.operadorId,
    required this.nomeOperador,
    required this.abertoEm,
    this.fechadoEm,
    required this.trocoInicial,
    required this.recebimentos,
    required this.saidas,
    required this.totalRecebido,
    required this.suprimentos,
    required this.sangrias,
    required this.dinheiroEsperado,
    this.dinheiroContado,
    this.diferenca,
    this.observacaoFechamento,
    required this.movimentos,
  });

  factory Caixa.deJson(Map<String, dynamic> json) => Caixa(
        id: json['id'] as String,
        patioId: json['patioId'] as String,
        nomePatio: json['nomePatio'] as String,
        operadorId: json['operadorId'] as String,
        nomeOperador: json['nomeOperador'] as String,
        abertoEm: _data(json['abertoEm']),
        fechadoEm: _dataOpcional(json['fechadoEm']),
        trocoInicial: _numero(json['trocoInicial']),
        recebimentos: ((json['recebimentos'] as List?) ?? [])
            .map((r) => RecebimentoPorForma.deJson(r as Map<String, dynamic>))
            .toList(),
        saidas: json['saidas'] as int,
        totalRecebido: _numero(json['totalRecebido']),
        suprimentos: _numero(json['suprimentos']),
        sangrias: _numero(json['sangrias']),
        dinheiroEsperado: _numero(json['dinheiroEsperado']),
        dinheiroContado: (json['dinheiroContado'] as num?)?.toDouble(),
        diferenca: (json['diferenca'] as num?)?.toDouble(),
        observacaoFechamento: json['observacaoFechamento'] as String?,
        movimentos: ((json['movimentos'] as List?) ?? []).map((m) => MovimentoCaixa.deJson(m as Map<String, dynamic>)).toList(),
      );
}

/// Convite de admin/operador aberto pelo link do e-mail (?ativar=token)
class ConviteUsuario {
  final String nomeCompleto;
  final String email;
  final String nomeEmpresa;
  final String perfil;
  final List<String> patios;

  ConviteUsuario({
    required this.nomeCompleto,
    required this.email,
    required this.nomeEmpresa,
    required this.perfil,
    required this.patios,
  });

  factory ConviteUsuario.deJson(Map<String, dynamic> json) => ConviteUsuario(
        nomeCompleto: json['nomeCompleto'] as String,
        email: json['email'] as String,
        nomeEmpresa: json['nomeEmpresa'] as String,
        perfil: json['perfil'] as String,
        patios: ((json['patios'] as List?) ?? []).cast<String>(),
      );
}

// ---------- Relatórios ----------

/// Data (sem hora) no formato da API: AAAA-MM-DD
String dataApi(DateTime data) =>
    '${data.year.toString().padLeft(4, '0')}-${data.month.toString().padLeft(2, '0')}-${data.day.toString().padLeft(2, '0')}';

/// Período do relatório (datas locais, fim inclusivo)
class PeriodoRelatorio {
  final DateTime de;
  final DateTime ate;

  PeriodoRelatorio(DateTime de, DateTime ate)
      : de = DateTime(de.year, de.month, de.day),
        ate = DateTime(ate.year, ate.month, ate.day);

  int get dias => ate.difference(de).inDays + 1;

  Map<String, String> paraConsulta() => {'de': dataApi(de), 'ate': dataApi(ate)};

  /// Usado no nome do arquivo exportado
  String get sufixo => '${dataApi(de)}_${dataApi(ate)}';
}

class MovimentoDia {
  final DateTime data;
  final int entradas;
  final int saidas;
  final double receitaAvulsos;
  final double receitaMensalidades;

  MovimentoDia({
    required this.data,
    required this.entradas,
    required this.saidas,
    required this.receitaAvulsos,
    required this.receitaMensalidades,
  });

  double get receita => receitaAvulsos + receitaMensalidades;

  factory MovimentoDia.deJson(Map<String, dynamic> json) => MovimentoDia(
        data: _data(json['data']),
        entradas: json['entradas'] as int,
        saidas: json['saidas'] as int,
        receitaAvulsos: _numero(json['receitaAvulsos']),
        receitaMensalidades: _numero(json['receitaMensalidades']),
      );
}

class IndicadoresPeriodo {
  final int entradas;
  final int saidas;
  final int saidasCobradas;
  final int saidasIsentas;
  final int saidasMensalistas;
  final int cancelamentos;
  final double receitaAvulsos;
  final double receitaMensalidades;
  final double receitaTotal;
  final double ticketMedio;
  final String permanenciaMedia;

  IndicadoresPeriodo({
    required this.entradas,
    required this.saidas,
    required this.saidasCobradas,
    required this.saidasIsentas,
    required this.saidasMensalistas,
    required this.cancelamentos,
    required this.receitaAvulsos,
    required this.receitaMensalidades,
    required this.receitaTotal,
    required this.ticketMedio,
    required this.permanenciaMedia,
  });

  factory IndicadoresPeriodo.deJson(Map<String, dynamic> json) => IndicadoresPeriodo(
        entradas: json['entradas'] as int,
        saidas: json['saidas'] as int,
        saidasCobradas: json['saidasCobradas'] as int,
        saidasIsentas: json['saidasIsentas'] as int,
        saidasMensalistas: json['saidasMensalistas'] as int,
        cancelamentos: json['cancelamentos'] as int,
        receitaAvulsos: _numero(json['receitaAvulsos']),
        receitaMensalidades: _numero(json['receitaMensalidades']),
        receitaTotal: _numero(json['receitaTotal']),
        ticketMedio: _numero(json['ticketMedio']),
        permanenciaMedia: json['permanenciaMedia'] as String,
      );
}

class DesempenhoPatio {
  final String id;
  final String nome;
  final bool ativo;
  final int vagas;
  final int entradas;
  final int saidas;
  final double receitaAvulsos;
  final double receitaMensalidades;
  final double giroPorVaga;

  DesempenhoPatio({
    required this.id,
    required this.nome,
    required this.ativo,
    required this.vagas,
    required this.entradas,
    required this.saidas,
    required this.receitaAvulsos,
    required this.receitaMensalidades,
    required this.giroPorVaga,
  });

  double get receita => receitaAvulsos + receitaMensalidades;

  factory DesempenhoPatio.deJson(Map<String, dynamic> json) => DesempenhoPatio(
        id: json['id'] as String,
        nome: json['nome'] as String,
        ativo: json['ativo'] as bool,
        vagas: json['vagas'] as int,
        entradas: json['entradas'] as int,
        saidas: json['saidas'] as int,
        receitaAvulsos: _numero(json['receitaAvulsos']),
        receitaMensalidades: _numero(json['receitaMensalidades']),
        giroPorVaga: _numero(json['giroPorVaga']),
      );
}

class DesempenhoOperador {
  final String id;
  final String nome;
  final int saidas;
  final int mensalidades;
  final double recebido;
  final int caixasFechados;
  final double diferencaCaixa;
  final int cancelamentos;

  DesempenhoOperador({
    required this.id,
    required this.nome,
    required this.saidas,
    required this.mensalidades,
    required this.recebido,
    required this.caixasFechados,
    required this.diferencaCaixa,
    required this.cancelamentos,
  });

  factory DesempenhoOperador.deJson(Map<String, dynamic> json) => DesempenhoOperador(
        id: json['id'] as String,
        nome: json['nome'] as String,
        saidas: json['saidas'] as int,
        mensalidades: json['mensalidades'] as int,
        recebido: _numero(json['recebido']),
        caixasFechados: json['caixasFechados'] as int,
        diferencaCaixa: _numero(json['diferencaCaixa']),
        cancelamentos: json['cancelamentos'] as int,
      );
}

class CancelamentoRelatorio {
  final DateTime canceladaEm;
  final String placa;
  final String nomePatio;
  final String? canceladaPor;
  final String? motivo;

  CancelamentoRelatorio({required this.canceladaEm, required this.placa, required this.nomePatio, this.canceladaPor, this.motivo});

  factory CancelamentoRelatorio.deJson(Map<String, dynamic> json) => CancelamentoRelatorio(
        canceladaEm: _data(json['canceladaEm']),
        placa: json['placa'] as String,
        nomePatio: json['nomePatio'] as String,
        canceladaPor: json['canceladaPor'] as String?,
        motivo: json['motivo'] as String?,
      );
}

class MensalistasRelatorio {
  final int ativos;
  final int emDia;
  final int vencidos;
  final int novos;
  final int pagamentos;

  MensalistasRelatorio({required this.ativos, required this.emDia, required this.vencidos, required this.novos, required this.pagamentos});

  factory MensalistasRelatorio.deJson(Map<String, dynamic> json) => MensalistasRelatorio(
        ativos: json['ativos'] as int,
        emDia: json['emDia'] as int,
        vencidos: json['vencidos'] as int,
        novos: json['novos'] as int,
        pagamentos: json['pagamentos'] as int,
      );
}

/// Relatório do dono: todos os pátios da empresa ou um só
class RelatorioEmpresa {
  final PeriodoRelatorio periodo;
  final String nomeEmpresa;
  final String? nomePatio;
  final IndicadoresPeriodo totais;
  final List<RecebimentoPorForma> porForma;
  final List<MovimentoDia> porDia;

  /// Entradas por hora local (índice 0–23)
  final List<int> entradasPorHora;
  final List<DesempenhoPatio> porPatio;
  final List<DesempenhoOperador> porOperador;
  final List<CancelamentoRelatorio> cancelamentos;
  final MensalistasRelatorio mensalistas;

  RelatorioEmpresa({
    required this.periodo,
    required this.nomeEmpresa,
    this.nomePatio,
    required this.totais,
    required this.porForma,
    required this.porDia,
    required this.entradasPorHora,
    required this.porPatio,
    required this.porOperador,
    required this.cancelamentos,
    required this.mensalistas,
  });

  factory RelatorioEmpresa.deJson(Map<String, dynamic> json) {
    final horas = List.filled(24, 0);
    for (final h in (json['entradasPorHora'] as List).cast<Map<String, dynamic>>()) {
      horas[h['hora'] as int] = h['entradas'] as int;
    }
    List<T> lista<T>(String chave, T Function(Map<String, dynamic>) conversor) =>
        (json[chave] as List).map((e) => conversor(e as Map<String, dynamic>)).toList();

    return RelatorioEmpresa(
      periodo: PeriodoRelatorio(_data(json['de']), _data(json['ate'])),
      nomeEmpresa: json['nomeEmpresa'] as String,
      nomePatio: json['nomePatio'] as String?,
      totais: IndicadoresPeriodo.deJson(json['totais'] as Map<String, dynamic>),
      porForma: lista('porForma', RecebimentoPorForma.deJson),
      porDia: lista('porDia', MovimentoDia.deJson),
      entradasPorHora: horas,
      porPatio: lista('porPatio', DesempenhoPatio.deJson),
      porOperador: lista('porOperador', DesempenhoOperador.deJson),
      cancelamentos: lista('cancelamentos', CancelamentoRelatorio.deJson),
      mensalistas: MensalistasRelatorio.deJson(json['mensalistas'] as Map<String, dynamic>),
    );
  }
}

class IndicadoresPlataforma {
  final int empresasAtivas;
  final int empresasNovas;
  final int empresasSemMovimento;
  final int patiosAtivos;
  final int vagas;
  final int usuariosAtivos;
  final int mensalistasAtivos;
  final int entradas;
  final double valorProcessado;
  final double receitaRecorrente;
  final double valorEmAberto;
  final int empresasInadimplentes;

  IndicadoresPlataforma({
    required this.empresasAtivas,
    required this.empresasNovas,
    required this.empresasSemMovimento,
    required this.patiosAtivos,
    required this.vagas,
    required this.usuariosAtivos,
    required this.mensalistasAtivos,
    required this.entradas,
    required this.valorProcessado,
    this.receitaRecorrente = 0,
    this.valorEmAberto = 0,
    this.empresasInadimplentes = 0,
  });

  factory IndicadoresPlataforma.deJson(Map<String, dynamic> json) => IndicadoresPlataforma(
        empresasAtivas: json['empresasAtivas'] as int,
        empresasNovas: json['empresasNovas'] as int,
        empresasSemMovimento: json['empresasSemMovimento'] as int,
        patiosAtivos: json['patiosAtivos'] as int,
        vagas: json['vagas'] as int,
        usuariosAtivos: json['usuariosAtivos'] as int,
        mensalistasAtivos: json['mensalistasAtivos'] as int,
        entradas: json['entradas'] as int,
        valorProcessado: _numero(json['valorProcessado']),
        receitaRecorrente: _numero(json['receitaRecorrente']),
        valorEmAberto: _numero(json['valorEmAberto']),
        empresasInadimplentes: (json['empresasInadimplentes'] as int?) ?? 0,
      );
}

class UsoEmpresa {
  final String id;
  final String nome;
  final bool ativa;
  final DateTime criadoEm;
  final int patios;
  final int vagas;
  final int usuarios;
  final int mensalistasAtivos;
  final int entradas;
  final double valorProcessado;
  final DateTime? ultimaEntrada;
  final String? nomePlano;
  final double mensalidadePkLots;
  final String situacaoCobranca;

  UsoEmpresa({
    required this.id,
    required this.nome,
    required this.ativa,
    required this.criadoEm,
    required this.patios,
    required this.vagas,
    required this.usuarios,
    required this.mensalistasAtivos,
    required this.entradas,
    required this.valorProcessado,
    this.ultimaEntrada,
    this.nomePlano,
    this.mensalidadePkLots = 0,
    this.situacaoCobranca = 'em_dia',
  });

  factory UsoEmpresa.deJson(Map<String, dynamic> json) => UsoEmpresa(
        id: json['id'] as String,
        nome: json['nome'] as String,
        ativa: json['ativa'] as bool,
        criadoEm: _data(json['criadoEm']),
        patios: json['patios'] as int,
        vagas: json['vagas'] as int,
        usuarios: json['usuarios'] as int,
        mensalistasAtivos: json['mensalistasAtivos'] as int,
        entradas: json['entradas'] as int,
        valorProcessado: _numero(json['valorProcessado']),
        ultimaEntrada: _dataOpcional(json['ultimaEntrada']),
        nomePlano: json['nomePlano'] as String?,
        mensalidadePkLots: _numero(json['mensalidadePkLots']),
        situacaoCobranca: (json['situacaoCobranca'] as String?) ?? 'em_dia',
      );
}

/// Relatório do superadmin: uso de cada empresa cliente
class RelatorioPlataforma {
  final PeriodoRelatorio periodo;
  final IndicadoresPlataforma totais;
  final List<MovimentoDia> porDia;
  final List<UsoEmpresa> empresas;

  RelatorioPlataforma({required this.periodo, required this.totais, required this.porDia, required this.empresas});

  factory RelatorioPlataforma.deJson(Map<String, dynamic> json) => RelatorioPlataforma(
        periodo: PeriodoRelatorio(_data(json['de']), _data(json['ate'])),
        totais: IndicadoresPlataforma.deJson(json['totais'] as Map<String, dynamic>),
        porDia: (json['porDia'] as List).map((e) => MovimentoDia.deJson(e as Map<String, dynamic>)).toList(),
        empresas: (json['empresas'] as List).map((e) => UsoEmpresa.deJson(e as Map<String, dynamic>)).toList(),
      );
}

// ---------- Planos e assinatura ----------

/// Recurso que um plano pode liberar (catálogo vindo da API)
class RecursoPlano {
  final String codigo;
  final String nome;
  final String descricao;

  RecursoPlano({required this.codigo, required this.nome, required this.descricao});

  factory RecursoPlano.deJson(Map<String, dynamic> json) =>
      RecursoPlano(codigo: json['codigo'] as String, nome: json['nome'] as String, descricao: json['descricao'] as String);
}

/// Códigos dos recursos que o app esconde quando o plano não inclui
class Recursos {
  static const lpr = 'lpr';
  static const mensalistas = 'mensalistas';
  static const appMensalista = 'app_mensalista';
  static const relatorios = 'relatorios';
  static const impressoraRede = 'impressora_rede';
}

class Plano {
  final String id;
  final String nome;
  final String? descricao;
  final double precoPorPatio;

  /// Limites (nulo: sem limite)
  final int? maximoPatios;
  final int? maximoVagasPorPatio;
  final int? maximoUsuarios;
  final List<String> recursos;
  final int diasCarencia;
  final bool ativo;
  final int empresas;

  Plano({
    required this.id,
    required this.nome,
    this.descricao,
    required this.precoPorPatio,
    this.maximoPatios,
    this.maximoVagasPorPatio,
    this.maximoUsuarios,
    required this.recursos,
    required this.diasCarencia,
    required this.ativo,
    this.empresas = 0,
  });

  factory Plano.deJson(Map<String, dynamic> json) => Plano(
        id: json['id'] as String,
        nome: json['nome'] as String,
        descricao: json['descricao'] as String?,
        precoPorPatio: _numero(json['precoPorPatio']),
        maximoPatios: json['maximoPatios'] as int?,
        maximoVagasPorPatio: json['maximoVagasPorPatio'] as int?,
        maximoUsuarios: json['maximoUsuarios'] as int?,
        recursos: (json['recursos'] as List).cast<String>(),
        diasCarencia: json['diasCarencia'] as int,
        ativo: json['ativo'] as bool,
        empresas: (json['empresas'] as int?) ?? 0,
      );

  Map<String, dynamic> paraJson() => {
        'nome': nome,
        'descricao': descricao,
        'precoPorPatio': precoPorPatio,
        'maximoPatios': maximoPatios,
        'maximoVagasPorPatio': maximoVagasPorPatio,
        'maximoUsuarios': maximoUsuarios,
        'recursos': recursos,
        'diasCarencia': diasCarencia,
        'ativo': ativo,
      };
}

/// Assinatura de uma empresa (visão do superadmin)
class AssinaturaEmpresa {
  final String empresaId;
  final String planoId;
  final String nomePlano;
  final double precoPorPatioPlano;
  final double? precoPorPatioNegociado;
  final int diaVencimento;
  final DateTime? testeAte;
  final String? observacoes;
  final int patiosAtivos;
  final double valorMensal;

  AssinaturaEmpresa({
    required this.empresaId,
    required this.planoId,
    required this.nomePlano,
    required this.precoPorPatioPlano,
    this.precoPorPatioNegociado,
    required this.diaVencimento,
    this.testeAte,
    this.observacoes,
    required this.patiosAtivos,
    required this.valorMensal,
  });

  factory AssinaturaEmpresa.deJson(Map<String, dynamic> json) => AssinaturaEmpresa(
        empresaId: json['empresaId'] as String,
        planoId: json['planoId'] as String,
        nomePlano: json['nomePlano'] as String,
        precoPorPatioPlano: _numero(json['precoPorPatioPlano']),
        precoPorPatioNegociado: (json['precoPorPatioNegociado'] as num?)?.toDouble(),
        diaVencimento: json['diaVencimento'] as int,
        testeAte: _dataOpcional(json['testeAte']),
        observacoes: json['observacoes'] as String?,
        patiosAtivos: json['patiosAtivos'] as int,
        valorMensal: _numero(json['valorMensal']),
      );
}

class UsoLimite {
  final int usado;
  final int? maximo; // nulo: sem limite

  UsoLimite(this.usado, this.maximo);

  factory UsoLimite.deJson(Map<String, dynamic> json) => UsoLimite(json['usado'] as int, json['maximo'] as int?);
}

/// O plano da empresa, visto pelo admin
class MinhaAssinatura {
  final String nomePlano;
  final String? descricaoPlano;
  final double precoPorPatio;
  final double valorMensal;
  final int diaVencimento;
  final DateTime? testeAte;
  final UsoLimite patios;
  final UsoLimite usuarios;
  final UsoLimite vagasNoMaiorPatio;
  final List<({String codigo, String nome, bool liberado})> recursos;

  MinhaAssinatura({
    required this.nomePlano,
    this.descricaoPlano,
    required this.precoPorPatio,
    required this.valorMensal,
    required this.diaVencimento,
    this.testeAte,
    required this.patios,
    required this.usuarios,
    required this.vagasNoMaiorPatio,
    required this.recursos,
  });

  factory MinhaAssinatura.deJson(Map<String, dynamic> json) => MinhaAssinatura(
        nomePlano: json['nomePlano'] as String,
        descricaoPlano: json['descricaoPlano'] as String?,
        precoPorPatio: _numero(json['precoPorPatio']),
        valorMensal: _numero(json['valorMensal']),
        diaVencimento: json['diaVencimento'] as int,
        testeAte: _dataOpcional(json['testeAte']),
        patios: UsoLimite.deJson(json['patios'] as Map<String, dynamic>),
        usuarios: UsoLimite.deJson(json['usuarios'] as Map<String, dynamic>),
        vagasNoMaiorPatio: UsoLimite.deJson(json['vagasNoMaiorPatio'] as Map<String, dynamic>),
        recursos: [
          for (final r in (json['recursos'] as List).cast<Map<String, dynamic>>())
            (codigo: r['codigo'] as String, nome: r['nome'] as String, liberado: r['liberado'] as bool),
        ],
      );
}

/// Fatura do aluguel do PkLots
class Fatura {
  static const aberta = 'aberta';
  static const vencida = 'vencida';
  static const paga = 'paga';
  static const cancelada = 'cancelada';

  final String id;
  final String empresaId;
  final String nomeEmpresa;
  final String tipo; // mensalidade, avulsa
  final String descricao;
  final double valor;
  final DateTime vencimento;
  final String situacao;
  final int diasEmAtraso;
  final int? patiosCobrados;
  final double? precoPorPatio;
  final DateTime? pagaEm;
  final String? formaPagamento;
  final double? valorPago;
  final String? observacao;
  final String? motivoCancelamento;
  final String? pixCopiaECola;

  Fatura({
    required this.id,
    required this.empresaId,
    required this.nomeEmpresa,
    required this.tipo,
    required this.descricao,
    required this.valor,
    required this.vencimento,
    required this.situacao,
    this.diasEmAtraso = 0,
    this.patiosCobrados,
    this.precoPorPatio,
    this.pagaEm,
    this.formaPagamento,
    this.valorPago,
    this.observacao,
    this.motivoCancelamento,
    this.pixCopiaECola,
  });

  bool get emAberto => situacao == aberta || situacao == vencida;

  factory Fatura.deJson(Map<String, dynamic> json) => Fatura(
        id: json['id'] as String,
        empresaId: json['empresaId'] as String,
        nomeEmpresa: json['nomeEmpresa'] as String,
        tipo: json['tipo'] as String,
        descricao: json['descricao'] as String,
        valor: _numero(json['valor']),
        vencimento: _data(json['vencimento']),
        situacao: json['situacao'] as String,
        diasEmAtraso: (json['diasEmAtraso'] as int?) ?? 0,
        patiosCobrados: json['patiosCobrados'] as int?,
        precoPorPatio: (json['precoPorPatio'] as num?)?.toDouble(),
        pagaEm: _dataOpcional(json['pagaEm']),
        formaPagamento: json['formaPagamento'] as String?,
        valorPago: (json['valorPago'] as num?)?.toDouble(),
        observacao: json['observacao'] as String?,
        motivoCancelamento: json['motivoCancelamento'] as String?,
        pixCopiaECola: json['pixCopiaECola'] as String?,
      );
}

/// Chave PIX do PkLots, usada nas faturas
class RecebimentoPlataforma {
  final String? chavePix;
  final String? nomeRecebedor;
  final String? cidadeRecebedor;

  RecebimentoPlataforma({this.chavePix, this.nomeRecebedor, this.cidadeRecebedor});

  factory RecebimentoPlataforma.deJson(Map<String, dynamic> json) => RecebimentoPlataforma(
        chavePix: json['chavePix'] as String?,
        nomeRecebedor: json['nomeRecebedor'] as String?,
        cidadeRecebedor: json['cidadeRecebedor'] as String?,
      );
}

/// Situação da empresa perante as faturas do PkLots (aviso no painel)
class SituacaoCobranca {
  static const emDia = 'em_dia';
  static const vencida = 'vencida';
  static const bloqueada = 'bloqueada';

  final String situacao;
  final int diasEmAtraso;
  final int diasParaBloqueio;
  final double valorVencido;

  SituacaoCobranca({required this.situacao, this.diasEmAtraso = 0, this.diasParaBloqueio = 0, this.valorVencido = 0});

  factory SituacaoCobranca.deJson(Map<String, dynamic> json) => SituacaoCobranca(
        situacao: json['situacao'] as String,
        diasEmAtraso: json['diasEmAtraso'] as int,
        diasParaBloqueio: json['diasParaBloqueio'] as int,
        valorVencido: _numero(json['valorVencido']),
      );
}

// ---------- Tabela de preço ----------

/// Regras de cobrança de um conjunto de dias (semana ou fim de semana)
class RegrasPreco {
  static const hora = 'hora';
  static const fracao = 'fracao';

  final String modo; // hora: proporcional ao minuto; fracao: primeiro período + frações
  final double valorHora;
  final int primeiroPeriodoMinutos;
  final double valorPrimeiroPeriodo;
  final int fracaoMinutos;
  final double valorFracao;
  final double? diariaMaxima;
  final double? valorPernoite;
  final int inicioPernoite;
  final int fimPernoite;

  const RegrasPreco({
    this.modo = hora,
    this.valorHora = 15,
    this.primeiroPeriodoMinutos = 60,
    this.valorPrimeiroPeriodo = 0,
    this.fracaoMinutos = 60,
    this.valorFracao = 0,
    this.diariaMaxima,
    this.valorPernoite,
    this.inicioPernoite = 20,
    this.fimPernoite = 8,
  });

  factory RegrasPreco.deJson(Map<String, dynamic> json) => RegrasPreco(
        modo: json['modo'] as String,
        valorHora: _numero(json['valorHora']),
        primeiroPeriodoMinutos: json['primeiroPeriodoMinutos'] as int,
        valorPrimeiroPeriodo: _numero(json['valorPrimeiroPeriodo']),
        fracaoMinutos: json['fracaoMinutos'] as int,
        valorFracao: _numero(json['valorFracao']),
        diariaMaxima: (json['diariaMaxima'] as num?)?.toDouble(),
        valorPernoite: (json['valorPernoite'] as num?)?.toDouble(),
        inicioPernoite: json['inicioPernoite'] as int,
        fimPernoite: json['fimPernoite'] as int,
      );

  Map<String, dynamic> paraJson() => {
        'modo': modo,
        'valorHora': valorHora,
        'primeiroPeriodoMinutos': primeiroPeriodoMinutos,
        'valorPrimeiroPeriodo': valorPrimeiroPeriodo,
        'fracaoMinutos': fracaoMinutos,
        'valorFracao': valorFracao,
        'diariaMaxima': diariaMaxima,
        'valorPernoite': valorPernoite,
        'inicioPernoite': inicioPernoite,
        'fimPernoite': fimPernoite,
      };
}

/// Tabela de preço do pátio (fimDeSemana nulo: sábado e domingo seguem a semana)
class TabelaPreco {
  final RegrasPreco semana;
  final RegrasPreco? fimDeSemana;
  final int minutosTolerancia;
  final String descricao;

  TabelaPreco({required this.semana, this.fimDeSemana, required this.minutosTolerancia, required this.descricao});

  factory TabelaPreco.deJson(Map<String, dynamic> json) => TabelaPreco(
        semana: RegrasPreco.deJson(json['semana'] as Map<String, dynamic>),
        fimDeSemana: json['fimDeSemana'] == null ? null : RegrasPreco.deJson(json['fimDeSemana'] as Map<String, dynamic>),
        minutosTolerancia: json['minutosTolerancia'] as int,
        descricao: json['descricao'] as String,
      );
}

class SimulacaoTarifa {
  final double valor;
  final String duracao;
  final bool isento;
  final bool fimDeSemana;
  final String? detalhamento;

  SimulacaoTarifa({required this.valor, required this.duracao, required this.isento, required this.fimDeSemana, this.detalhamento});

  factory SimulacaoTarifa.deJson(Map<String, dynamic> json) => SimulacaoTarifa(
        valor: _numero(json['valor']),
        duracao: json['duracao'] as String,
        isento: json['isento'] as bool,
        fimDeSemana: json['fimDeSemana'] as bool,
        detalhamento: json['detalhamento'] as String?,
      );
}

// ---------- Auditoria e logs do sistema ----------

class OpcaoFiltro {
  final String id;
  final String nome;

  OpcaoFiltro({required this.id, required this.nome});

  factory OpcaoFiltro.deJson(Map<String, dynamic> json) => OpcaoFiltro(id: json['id'] as String, nome: json['nome'] as String);
}

class OpcoesAuditoria {
  final List<OpcaoFiltro> categorias;
  final List<OpcaoFiltro> patios;
  final List<OpcaoFiltro> usuarios;
  final List<OpcaoFiltro> empresas;

  OpcoesAuditoria({required this.categorias, required this.patios, required this.usuarios, required this.empresas});

  static List<OpcaoFiltro> _opcoes(dynamic lista) =>
      [for (final o in (lista as List? ?? const [])) OpcaoFiltro.deJson(o as Map<String, dynamic>)];

  factory OpcoesAuditoria.deJson(Map<String, dynamic> json) => OpcoesAuditoria(
        categorias: _opcoes(json['categorias']),
        patios: _opcoes(json['patios']),
        usuarios: _opcoes(json['usuarios']),
        empresas: _opcoes(json['empresas']),
      );
}

class FiltroAuditoria {
  final PeriodoRelatorio periodo;
  final String? empresaId;
  final String? patioId;
  final String? categoria;
  final String? situacao;
  final String? placa;
  final String? usuarioId;

  const FiltroAuditoria(
      {required this.periodo, this.empresaId, this.patioId, this.categoria, this.situacao, this.placa, this.usuarioId});

  Map<String, String> paraConsulta() => {
        ...periodo.paraConsulta(),
        'empresaId': ?empresaId,
        'patioId': ?patioId,
        'categoria': ?categoria,
        'situacao': ?situacao,
        if (placa != null && placa!.trim().isNotEmpty) 'placa': placa!.trim(),
        'usuarioId': ?usuarioId,
      };
}

class DetalheAuditoria {
  final String rotulo;
  final String valor;

  DetalheAuditoria({required this.rotulo, required this.valor});

  factory DetalheAuditoria.deJson(Map<String, dynamic> json) =>
      DetalheAuditoria(rotulo: json['rotulo'] as String, valor: json['valor'] as String);
}

class RegistroAuditoria {
  static const sucesso = 'sucesso';
  static const alerta = 'alerta';
  static const erro = 'erro';

  final String id;
  final DateTime criadoEm;
  final String acao;
  final String descricao;
  final String categoria;
  final String situacao;
  final String? placa;
  final String patio;
  final String? empresa;
  final String usuario;
  final List<DetalheAuditoria> detalhes;

  RegistroAuditoria({
    required this.id,
    required this.criadoEm,
    required this.acao,
    required this.descricao,
    required this.categoria,
    required this.situacao,
    this.placa,
    required this.patio,
    this.empresa,
    required this.usuario,
    required this.detalhes,
  });

  factory RegistroAuditoria.deJson(Map<String, dynamic> json) => RegistroAuditoria(
        id: json['id'] as String,
        criadoEm: _data(json['criadoEm']),
        acao: json['acao'] as String,
        descricao: json['descricao'] as String,
        categoria: json['categoria'] as String,
        situacao: json['situacao'] as String,
        placa: json['placa'] as String?,
        patio: json['patio'] as String,
        empresa: json['empresa'] as String?,
        usuario: json['usuario'] as String,
        detalhes: [for (final d in (json['detalhes'] as List? ?? const [])) DetalheAuditoria.deJson(d as Map<String, dynamic>)],
      );
}

class PaginaAuditoria {
  final List<RegistroAuditoria> itens;
  final int total;
  final int pagina;
  final int tamanhoPagina;

  PaginaAuditoria({required this.itens, required this.total, required this.pagina, required this.tamanhoPagina});

  bool get temMais => pagina * tamanhoPagina < total;

  factory PaginaAuditoria.deJson(Map<String, dynamic> json) => PaginaAuditoria(
        itens: [for (final i in json['itens'] as List) RegistroAuditoria.deJson(i as Map<String, dynamic>)],
        total: json['total'] as int,
        pagina: json['pagina'] as int,
        tamanhoPagina: json['tamanhoPagina'] as int,
      );
}

class RegistroLogSistema {
  final DateTime quando;
  final String nivel; // Information, Warning, Error, Fatal
  final String mensagem;
  final String? excecao;
  final String? origem;
  final String? caminho;
  final int? status;

  RegistroLogSistema(
      {required this.quando, required this.nivel, required this.mensagem, this.excecao, this.origem, this.caminho, this.status});

  factory RegistroLogSistema.deJson(Map<String, dynamic> json) => RegistroLogSistema(
        quando: _data(json['quando']),
        nivel: json['nivel'] as String,
        mensagem: json['mensagem'] as String,
        excecao: json['excecao'] as String?,
        origem: json['origem'] as String?,
        caminho: json['caminho'] as String?,
        status: json['status'] as int?,
      );
}

class PaginaLogsSistema {
  final List<RegistroLogSistema> itens;
  final int total;
  final int pagina;
  final int tamanhoPagina;
  final bool disponivel;

  PaginaLogsSistema(
      {required this.itens, required this.total, required this.pagina, required this.tamanhoPagina, required this.disponivel});

  bool get temMais => pagina * tamanhoPagina < total;

  factory PaginaLogsSistema.deJson(Map<String, dynamic> json) => PaginaLogsSistema(
        itens: [for (final i in json['itens'] as List) RegistroLogSistema.deJson(i as Map<String, dynamic>)],
        total: json['total'] as int,
        pagina: json['pagina'] as int,
        tamanhoPagina: json['tamanhoPagina'] as int,
        disponivel: json['disponivel'] as bool,
      );
}
