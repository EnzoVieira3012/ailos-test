using Ailos.Tarifa.Worker.Domain.Entities;

namespace Ailos.Tarifa.Worker.Infrastructure.Repositories;

public interface ITarifaRepository
{
    Task<long> InserirTarifaAsync(TarifaEntity tarifa, CancellationToken cancellationToken = default);
    Task RegistrarProcessamentoAsync(TarifaProcessadaEntity historico, CancellationToken cancellationToken = default);
    Task<bool> TransferenciaJaProcessadaAsync(long transferenciaId, string topico, long offset, CancellationToken cancellationToken = default);
    Task<IEnumerable<TarifaProcessadaEntity>> ObterHistoricoAsync(DateTime dataInicio, DateTime dataFim, CancellationToken cancellationToken = default);
}
