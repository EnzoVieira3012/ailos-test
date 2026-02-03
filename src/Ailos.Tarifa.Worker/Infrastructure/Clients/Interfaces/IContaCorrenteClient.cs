namespace Ailos.Tarifa.Worker.Infrastructure.Clients.Interfaces;

public interface IContaCorrenteClient
{
    Task<bool> AplicarTarifaAsync(
        long contaId, 
        long transferenciaId, 
        decimal valorTarifa, 
        CancellationToken cancellationToken = default);
}
