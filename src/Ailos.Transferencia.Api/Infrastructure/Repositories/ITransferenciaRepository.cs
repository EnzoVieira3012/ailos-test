using Ailos.Transferencia.Api.Domain.Entities;

namespace Ailos.Transferencia.Api.Infrastructure.Repositories;

public interface ITransferenciaRepository
{
    Task<TransferenciaEntity?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TransferenciaEntity> InserirAsync(TransferenciaEntity transferencia, CancellationToken cancellationToken = default);
    Task AtualizarAsync(TransferenciaEntity transferencia, CancellationToken cancellationToken = default);
    Task<IEnumerable<TransferenciaEntity>> ObterPorContaAsync(long contaId, CancellationToken cancellationToken = default);
}
