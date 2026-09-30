namespace PkLots.Application.Servicos
{
    public static class FusoHorario
    {
        // Fuso usado para "hoje" nos relatórios; cai para UTC-3 fixo se o sistema não tiver tzdata
        public static readonly TimeZoneInfo Brasil = Carregar();

        private static TimeZoneInfo Carregar()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
            }
        }

        // Início do dia local (em UTC) que contém o instante informado
        public static DateTime InicioDoDiaUtc(DateTime agoraUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, Brasil);
            var inicioLocal = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Brasil);
        }
    }
}
