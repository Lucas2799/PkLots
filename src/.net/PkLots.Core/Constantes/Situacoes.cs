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
        public const string Erro = "erro";
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
    }

    public static class SituacaoAuditoria
    {
        public const string Sucesso = "sucesso";
        public const string Erro = "erro";
        public const string Alerta = "alerta";
    }
}
