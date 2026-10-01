namespace PkLots.Api.Middleware
{
    // Usuário com senha temporária/pública só pode trocar a senha (o resto responde 403 com código "troca_senha")
    public class TrocaSenhaObrigatoria(RequestDelegate proximo)
    {
        public const string Claim = "deve_trocar_senha";

        private static readonly string[] Liberados = ["/api/auth/trocar-senha", "/api/auth/login", "/health"];

        public async Task InvokeAsync(HttpContext contexto)
        {
            var precisaTrocar = contexto.User.HasClaim(Claim, "true");
            var caminho = contexto.Request.Path.Value ?? "";

            if (precisaTrocar && caminho.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
                && !Liberados.Any(l => caminho.Equals(l, StringComparison.OrdinalIgnoreCase)))
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                await contexto.Response.WriteAsJsonAsync(new
                {
                    title = "Troca de senha obrigatória",
                    status = 403,
                    detail = "Defina uma nova senha para continuar.",
                    codigo = "troca_senha"
                });
                return;
            }

            await proximo(contexto);
        }
    }
}
