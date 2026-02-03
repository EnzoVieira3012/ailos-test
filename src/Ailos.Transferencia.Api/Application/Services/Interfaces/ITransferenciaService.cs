using Ailos.Transferencia.Api.Application.DTOs.Transferencia;
using Ailos.Transferencia.Api.Domain.Entities;

namespace Ailos.Transferencia.Api.Application.Services;

public interface ITransferenciaService
{
    Task<TransferenciaResponse> CriarTransferenciaAsync(
        long contaIdUsuarioLogado,
        TransferenciaRequest request,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<TransferenciaEntity>> ObterTransferenciasPorContaAsync(
        long contaId,
        CancellationToken cancellationToken = default);
}
