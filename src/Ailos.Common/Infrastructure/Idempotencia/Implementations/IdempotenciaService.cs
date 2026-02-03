using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ailos.Common.Infrastructure.Implementations.Idempotencia;

public interface IIdempotenciaService
{
    Task<T?> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    Task<bool> ExistsAsync(string key);
    Task RemoveAsync(string key);
    Task<T?> GetAsync<T>(string key);
}

public sealed class IdempotenciaService : IIdempotenciaService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<IdempotenciaService> _logger;

    public IdempotenciaService(IMemoryCache cache, ILogger<IdempotenciaService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        _logger.LogDebug("Verificando idempotência para chave: {Key}", key);

        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            _logger.LogDebug("Chave encontrada em cache: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Chave não encontrada, executando factory: {Key}", key);
        var value = await factory();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            Size = 1
        };

        if (expiration.HasValue)
        {
            cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
        }
        else
        {
            // Default: 24 horas
            cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
        }

        _cache.Set(key, value, cacheOptions);
        _logger.LogInformation("Chave armazenada em cache: {Key}", key);

        return value;
    }

    public Task<bool> ExistsAsync(string key)
    {
        var exists = _cache.TryGetValue(key, out _);
        _logger.LogDebug("Verificação de existência para {Key}: {Exists}", key, exists);
        return Task.FromResult(exists);
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        _logger.LogInformation("Chave removida do cache: {Key}", key);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        _cache.TryGetValue(key, out T? value);
        _logger.LogDebug("Recuperado valor para chave {Key}: {HasValue}", key, value != null);
        return Task.FromResult(value);
    }
}