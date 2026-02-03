using Ailos.Transferencia.Api.Domain.Entities;
using Ailos.Transferencia.Api.Infrastructure.Repositories;

namespace Ailos.Transferencia.Api.Application.Services;

public sealed class IdempotenciaService : IIdempotenciaService
{
    private readonly IIdempotenciaRepository _repository;

    public IdempotenciaService(IIdempotenciaRepository repository)
    {
        _repository = repository;
    }

    public async Task<Idempotencia?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default)
    {
        return await _repository.ObterPorChaveAsync(chave, cancellationToken);
    }

    public async Task RegistrarAsync(string chave, string? requisicao, string? resultado, CancellationToken cancellationToken = default)
    {
        await _repository.RegistrarAsync(chave, requisicao, resultado, cancellationToken);
    }

    public async Task<string?> ObterResultadoAsync(string chave, CancellationToken cancellationToken = default)
    {
        var idempotencia = await _repository.ObterPorChaveAsync(chave, cancellationToken);
        return idempotencia?.Resultado;
    }

    public async Task<bool> RequisicaoJaProcessadaAsync(string chave, CancellationToken cancellationToken = default)
    {
        var idempotencia = await _repository.ObterPorChaveAsync(chave, cancellationToken);
        return idempotencia != null && !string.IsNullOrEmpty(idempotencia.Resultado);
    }
}
