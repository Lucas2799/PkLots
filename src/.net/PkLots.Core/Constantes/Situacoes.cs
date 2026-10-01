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
        public const string Erro = "erro";
    }

    public static class SituacaoPagamento
    {
        public const string Calculado = "calculado";
        public const string Pago = "pago";
        public const string Isento = "isento"; // saída dentro da tolerância
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

    public static class AcoesAuditoria
    {
        public const string EntradaSucesso = "entrada_sucesso";
        public const string EntradaRecusada = "entrada_recusada";
        public const string SaidaSucesso = "saida_sucesso";
        public const string SaidaSemEntrada = "saida_sem_entrada";
        public const string LprFalhou = "lpr_falhou";
        public const string VagaManutencao = "vaga_manutencao";
        public const string VagaLiberada = "vaga_liberada";
        public const string ConfiguracaoAlterada = "configuracao_alterada";
        public const string QuantidadeVagasAlterada = "quantidade_vagas_alterada";
        public const string UsuarioCriado = "usuario_criado";
        public const string UsuarioAlterado = "usuario_alterado";
        public const string SenhaRedefinida = "senha_redefinida";
        public const string ConfiguracaoImpressaoAlterada = "configuracao_impressao_alterada";
        public const string ImpressaoFalhou = "impressao_falhou";
    }

    public static class SituacaoAuditoria
    {
        public const string Sucesso = "sucesso";
        public const string Erro = "erro";
        public const string Alerta = "alerta";
    }
}
