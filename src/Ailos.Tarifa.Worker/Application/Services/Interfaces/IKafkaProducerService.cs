namespace Ailos.Tarifa.Worker.Application.Services;

public interface IKafkaProducerService
{
    Task ProduzirMensagemAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default);
}
