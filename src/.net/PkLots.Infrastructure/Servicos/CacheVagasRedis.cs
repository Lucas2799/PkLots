using System.Text.Json;
using Microsoft.Extensions.Logging;
using PkLots.Application.DTOs;
using PkLots.Application.Interfaces;
using StackExchange.Redis;

namespace PkLots.Infrastructure.Servicos
{
    // Cache do resumo de vagas no Redis. Falhas do Redis não derrubam a API:
    // o resumo passa a ser calculado direto no banco.
    public class CacheVagasRedis(IConnectionMultiplexer redis, ILogger<CacheVagasRedis> logger) : ICacheVagas
    {
        private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

        private static string Chave(Guid patioId) => $"pklots:patio:{patioId}:vagas:resumo";

        public async Task<ResumoVagasDto?> ObterAsync(Guid patioId)
        {
            try
            {
                var valor = await redis.GetDatabase().StringGetAsync(Chave(patioId));
                return valor.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ResumoVagasDto>(valor.ToString());
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                logger.LogWarning(ex, "Redis indisponível ao ler resumo de vagas");
                return null;
            }
        }

        public async Task DefinirAsync(Guid patioId, ResumoVagasDto resumo)
        {
            try
            {
                await redis.GetDatabase().StringSetAsync(Chave(patioId), JsonSerializer.Serialize(resumo), Validade);
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                logger.LogWarning(ex, "Redis indisponível ao gravar resumo de vagas");
            }
        }

        public async Task InvalidarAsync(Guid patioId)
        {
            try
            {
                await redis.GetDatabase().KeyDeleteAsync(Chave(patioId));
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                logger.LogWarning(ex, "Redis indisponível ao invalidar resumo de vagas");
            }
        }
    }

    // Usado quando nenhuma conexão Redis está configurada
    public class CacheVagasNulo : ICacheVagas
    {
        public Task<ResumoVagasDto?> ObterAsync(Guid patioId) => Task.FromResult<ResumoVagasDto?>(null);
        public Task DefinirAsync(Guid patioId, ResumoVagasDto resumo) => Task.CompletedTask;
        public Task InvalidarAsync(Guid patioId) => Task.CompletedTask;
    }
}
