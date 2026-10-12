namespace PkLots.Core.Constantes
{
    public static class SituacaoVaga
    {
        public const string Disponivel = "disponivel";
        public const string Ocupada = "ocupada";
        public const string Manutencao = "manutencao";
    }

    public static class SituacaoEntrada
    {
        public const string Ativa = "ativa";
        public const string Concluida = "concluida";
        public const string Cancelada = "cancelada";
        public const string Erro = "erro";

        // Operador pode cancelar só logo após a entrada (corrigir placa digitada errada); depois, só o admin
        public const int MinutosCancelamentoOperador = 10;
        public const int TamanhoMaximoMotivo = 200;
    }

    public static class SituacaoPagamento
    {
        public const string Calculado = "calculado";
        public const string Pago = "pago";
        public const string Isento = "isento"; // saída dentro da tolerância
        public const string Mensalista = "mensalista"; // mensalista em dia: sem cobrança
        public const string Erro = "erro";
    }

    public static class ModoImpressao
    {
        public const string Desativada = "desativada";
        public const string Rede = "rede";           // impressora térmica ESC/POS na rede (TCP)
        public const string Navegador = "navegador"; // PDF no tamanho do papel, impresso pelo navegador

        public static readonly string[] Todos = [Desativada, Rede, Navegador];
    }

    // Resultado da impressão automática informado ao app após uma entrada
    public static class ResultadoImpressao
    {
        public const string Impresso = "impresso";
        public const string Falhou = "falhou";
        public const string Navegador = "navegador"; // o app deve imprimir pelo navegador
    }

    public static class SituacaoMensalista
    {
        public const string EmDia = "em_dia";
        public const string Vencendo = "vencendo"; // em dia, mas vence nos próximos dias
        public const string Vencido = "vencido";
        public const string Inativo = "inativo";
        public const int DiasAvisoVencimento = 5;
    }

    public static class FormasPagamento
    {
        public const string Dinheiro = "dinheiro";
        public const string Pix = "pix";
        public const string Cartao = "cartao";
        public const string Outro = "outro";

        // Mensalidade aceita também "outro" (ex.: transferência); a saída aceita as formas habilitadas pela empresa
        public static readonly string[] Todas = [Dinheiro, Pix, Cartao, Outro];
        public static readonly string[] Saida = [Dinheiro, Pix, Cartao];
    }

    public static class ProvedoresPagamento
    {
        public const string PixManual = "pix_manual";
        public static readonly string[] Todos = [PixManual];
    }

    public static class SituacaoCobranca
    {
        public const string Pendente = "pendente";
        public const string Paga = "paga";
        public const string Cancelada = "cancelada";
    }

    public static class TipoMovimentoCaixa
    {
        public const string Sangria = "sangria";
        public const string Suprimento = "suprimento";
    }

    public static class AcoesAuditoria
    {
        public const string EntradaSucesso = "entrada_sucesso";
        public const string EntradaRecusada = "entrada_recusada";
        public const string EntradaCancelada = "entrada_cancelada";
        public const string ConviteMensalistaEnviado = "convite_mensalista_enviado";
        public const string ConviteMensalistaAceito = "convite_mensalista_aceito";
        public const string AcessoMensalistaRevogado = "acesso_mensalista_revogado";
        public const string SaidaSucesso = "saida_sucesso";
        public const string SaidaSemEntrada = "saida_sem_entrada";
        public const string LprFalhou = "lpr_falhou";
        public const string VagaManutencao = "vaga_manutencao";
        public const string VagaLiberada = "vaga_liberada";
        public const string ConfiguracaoAlterada = "configuracao_alterada";
        public const string QuantidadeVagasAlterada = "quantidade_vagas_alterada";
        public const string UsuarioCriado = "usuario_criado";
        public const string UsuarioAlterado = "usuario_alterado";
        public const string SenhaRedefinida = "senha_redefinida"; // link de nova senha enviado pelo admin
        public const string ConviteUsuarioEnviado = "convite_usuario_enviado";
        public const string MensalistaCriado = "mensalista_criado";
        public const string MensalistaAlterado = "mensalista_alterado";
        public const string MensalidadePaga = "mensalidade_paga";
        public const string CaixaAberto = "caixa_aberto";
        public const string CaixaFechado = "caixa_fechado";
        public const string MovimentoCaixa = "movimento_caixa";
        public const string ConfiguracaoPagamentoAlterada = "configuracao_pagamento_alterada";
        public const string EmpresaCriada = "empresa_criada";
        public const string EmpresaAlterada = "empresa_alterada";
        public const string AssinaturaAlterada = "assinatura_alterada";
        public const string FaturaPaga = "fatura_paga";
        public const string FaturaCancelada = "fatura_cancelada";
        public const string PatioCriado = "patio_criado";
        public const string PatioAlterado = "patio_alterado";
        public const string ConfiguracaoImpressaoAlterada = "configuracao_impressao_alterada";
        public const string ImpressaoFalhou = "impressao_falhou";
    }

    public static class TiposToken
    {
        public const string ConviteMensalista = "convite_mensalista";
        public const string ConviteUsuario = "convite_usuario"; // admin/operador criado pela empresa ou pela plataforma
        public const string RedefinirSenha = "redefinir_senha";

        public static readonly TimeSpan ValidadeConvite = TimeSpan.FromHours(72);
        public static readonly TimeSpan ValidadeRedefinicao = TimeSpan.FromHours(1);
    }

    // Situação do acesso do mensalista ao app (mostrada ao operador)
    public static class AcessoAppMensalista
    {
        public const string SemEmail = "sem_email";
        public const string ConvitePendente = "convite_pendente";
        public const string Ativo = "ativo";
    }

    public static class SituacaoAuditoria
    {
        public const string Sucesso = "sucesso";
        public const string Erro = "erro";
        public const string Alerta = "alerta";
    }

    public static class SituacaoFatura
    {
        public const string Aberta = "aberta";
        public const string Paga = "paga";
        public const string Cancelada = "cancelada";
        public const string Vencida = "vencida"; // calculada: aberta com vencimento passado (não é gravada)
    }

    // Situação da empresa perante as faturas do PkLots
    public static class SituacaoAssinatura
    {
        public const string EmDia = "em_dia";
        public const string Vencida = "vencida";     // dentro da carência: só avisos
        public const string Bloqueada = "bloqueada"; // carência esgotada: bloqueio parcial até pagar

        // Avisos por e-mail de cada fatura (cada um sai uma vez)
        public const string AvisoLembrete = "lembrete";     // alguns dias antes do vencimento
        public const string AvisoVencimento = "vencimento"; // venceu
        public const string AvisoBloqueio = "bloqueio";     // entrou em bloqueio parcial
        public const int DiasLembrete = 5;
    }

    public static class TipoFatura
    {
        public const string Mensalidade = "mensalidade";
        public const string Avulsa = "avulsa";

        // Emitida alguns dias antes do vencimento, para dar tempo de pagar
        public const int DiasAntecedenciaEmissao = 10;
        public const int TamanhoMaximoDescricao = 120;
    }

    // Recursos que um plano pode liberar. Cada código tem um ponto de bloqueio no sistema;
    // um recurso novo entra aqui e no lugar onde deve ser barrado, e aparece sozinho no cadastro de planos.
    public static class RecursosPlano
    {
        public const string Lpr = "lpr";
        public const string Mensalistas = "mensalistas";
        public const string AppMensalista = "app_mensalista";
        public const string Relatorios = "relatorios";
        public const string ImpressoraRede = "impressora_rede";

        public static readonly IReadOnlyList<(string Codigo, string Nome, string Descricao)> Catalogo =
        [
            (Lpr, "Leitura automática de placa", "Entrada e saída pela foto do veículo"),
            (Mensalistas, "Mensalistas", "Cadastro de mensalistas e vagas reservadas"),
            (AppMensalista, "App do mensalista", "Convite por e-mail para o mensalista acompanhar no app"),
            (Relatorios, "Relatórios", "Relatório gerencial por período, com exportação em PDF"),
            (ImpressoraRede, "Impressora térmica de rede", "Impressão automática do ticket na impressora do pátio"),
        ];

        public static readonly string[] Todos = Catalogo.Select(r => r.Codigo).ToArray();

        public static string Nome(string codigo) => Catalogo.FirstOrDefault(r => r.Codigo == codigo).Nome ?? codigo;

        // Plano dado às empresas que já existiam: tudo liberado, sem limites e sem cobrança
        public static readonly Guid PlanoCortesiaId = new("0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0001");
    }
}
