using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories.Interfaces;
using Ailos.ContaCorrente.Api.Application.Services.Interfaces;

namespace Ailos.ContaCorrente.Api.Application.Services.Implementations;

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
