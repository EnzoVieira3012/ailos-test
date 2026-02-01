using Ailos.ContaCorrente.Api.Domain.Entities;

namespace Ailos.ContaCorrente.Api.Application.Services.Interfaces;

public interface IIdempotenciaService
{
    Task<Idempotencia?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default);
    Task RegistrarAsync(string chave, string? requisicao, string? resultado, CancellationToken cancellationToken = default);
    Task<string?> ObterResultadoAsync(string chave, CancellationToken cancellationToken = default);
    Task<bool> RequisicaoJaProcessadaAsync(string chave, CancellationToken cancellationToken = default);
}
