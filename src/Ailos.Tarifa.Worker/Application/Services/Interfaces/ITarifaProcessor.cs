namespace Ailos.Tarifa.Worker.Application.Services;

public interface ITarifaProcessor
{
    Task<bool> ProcessarMensagemAsync(
        string mensagemJson, 
        string topico, 
        int partition, 
        long offset, 
        CancellationToken cancellationToken = default);
}
