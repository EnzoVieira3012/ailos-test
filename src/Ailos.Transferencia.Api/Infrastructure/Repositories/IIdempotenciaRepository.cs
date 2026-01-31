using Ailos.Transferencia.Api.Domain.Entities;

namespace Ailos.Transferencia.Api.Infrastructure.Repositories;

public interface IIdempotenciaRepository
{
    Task<Idempotencia?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default);
    Task RegistrarAsync(string chave, string? requisicao, string? resultado, CancellationToken cancellationToken = default);
}
