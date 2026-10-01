using Microsoft.EntityFrameworkCore;
using PkLots.Application.Interfaces;
using PkLots.Infrastructure.Data;

// Comandos executados no servidor/container, fora da API (ex.: criar o superusuário)
public static class ComandosServidor
{
    public static async Task<int> CriarSuperAdminAsync(IServiceProvider servicos, string[] args)
    {
        var indice = Array.IndexOf(args, "--email");
        var email = indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("Uso: PkLots.Api criar-superadmin --email voce@empresa.com");
            return 2;
        }

        using var escopo = servicos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PkLotsDbContext>();
        await db.Database.MigrateAsync();

        try
        {
            var (senha, criado) = await SemeadorDados.CriarOuRedefinirSuperAdminAsync(
                db, escopo.ServiceProvider.GetRequiredService<IHashSenha>(), email);

            Console.WriteLine();
            Console.WriteLine(criado ? "Superusuário criado." : "Senha do superusuário redefinida.");
            Console.WriteLine($"  E-mail:           {email.Trim().ToLowerInvariant()}");
            Console.WriteLine($"  Senha temporária: {senha}");
            Console.WriteLine("  A senha aparece só agora e precisa ser trocada no primeiro acesso.");
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or PkLots.Application.Excecoes.ExcecaoNegocio)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            return 1;
        }
    }
}
