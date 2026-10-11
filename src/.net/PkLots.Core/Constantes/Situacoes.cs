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
}
