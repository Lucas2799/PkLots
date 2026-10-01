using System.Text;
using PkLots.Infrastructure.Seguranca;

// Fora de desenvolvimento a API não sobe com segredos ausentes ou conhecidos (valores de exemplo do projeto)
public static class ValidacaoConfiguracao
{
    private static readonly string[] SegredosConhecidos =
    [
        "sua_chave_secreta_super_segura_minimo_32_caracteres_aleatorios",
        "troque_por_uma_chave_aleatoria_com_pelo_menos_32_caracteres",
        "SenhaForteAqui123!"
    ];

    public static void Validar(IConfiguration configuracao, IHostEnvironment ambiente, OpcoesJwt jwt)
    {
        var problemas = new List<string>();

        if (Encoding.UTF8.GetByteCount(jwt.SecretKey ?? "") < 32)
            problemas.Add("Jwt__SecretKey ausente ou com menos de 32 caracteres (gere com: openssl rand -base64 48).");

        var conexao = configuracao.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conexao))
            problemas.Add("ConnectionStrings__DefaultConnection não configurada.");

        if (!ambiente.IsDevelopment())
        {
            if (SegredosConhecidos.Any(s => jwt.SecretKey?.Contains(s) == true))
                problemas.Add("Jwt__SecretKey usa o valor de exemplo do projeto. Gere uma chave aleatória.");
            if (conexao is not null && SegredosConhecidos.Any(conexao.Contains))
                problemas.Add("A senha do banco usa o valor de exemplo do projeto. Defina POSTGRES_SENHA no .env.");
        }

        if (problemas.Count > 0)
            throw new InvalidOperationException("Configuração insegura ou incompleta:\n - " + string.Join("\n - ", problemas));
    }
}
