using PkLots.Application.Interfaces;
using PkLots.Application.Servicos;

namespace PkLots.Api.Endpoints
{
    // Bloqueio parcial por fatura vencida além da carência: na administração só as consultas seguem liberadas
    public static class FiltroBloqueioCobranca
    {
        public static async ValueTask<object?> SomenteConsultas(EndpointFilterInvocationContext contexto, EndpointFilterDelegate proximo)
        {
            var http = contexto.HttpContext;
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                var servicos = http.RequestServices;
                await Inadimplencia.ExigirSemBloqueioAsync(
                    servicos.GetRequiredService<IPkLotsDbContext>(),
                    servicos.GetRequiredService<IContextoPatio>().EmpresaId,
                    servicos.GetRequiredService<TimeProvider>(),
                    "Mudanças na administração",
                    http.RequestAborted);
            }
            return await proximo(contexto);
        }
    }
}
