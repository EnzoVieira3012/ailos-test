using Ailos.ContaCorrente.Api.Domain.Entities;

namespace Ailos.ContaCorrente.Api.Infrastructure.Repositories;

public interface IIdempotenciaRepository
{
    Task<Idempotencia?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default);
    Task RegistrarAsync(string chave, string? requisicao, string? resultado, CancellationToken cancellationToken = default);
}
