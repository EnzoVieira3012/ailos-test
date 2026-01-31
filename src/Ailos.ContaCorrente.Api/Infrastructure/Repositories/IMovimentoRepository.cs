using Ailos.ContaCorrente.Api.Domain.Entities;

namespace Ailos.ContaCorrente.Api.Infrastructure.Repositories;

public interface IMovimentoRepository
{
    Task<Movimento> InserirAsync(Movimento movimento, CancellationToken cancellationToken = default);
    Task<IEnumerable<Movimento>> ObterPorContaAsync(long contaId, CancellationToken cancellationToken = default);
    Task<decimal> CalcularSaldoAsync(long contaId, CancellationToken cancellationToken = default);
}
