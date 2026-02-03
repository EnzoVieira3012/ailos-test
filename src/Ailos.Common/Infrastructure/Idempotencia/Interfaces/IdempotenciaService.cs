namespace Ailos.Common.Infrastructure.Interfaces.Idempotencia;

public interface IIdempotenciaService
{
    Task<T?> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    Task<bool> ExistsAsync(string key);
    Task RemoveAsync(string key);
    Task<T?> GetAsync<T>(string key);
}
