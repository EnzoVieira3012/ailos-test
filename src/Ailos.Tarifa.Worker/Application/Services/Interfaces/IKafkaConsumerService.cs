namespace Ailos.Tarifa.Worker.Application.Services;

public interface IKafkaConsumerService
{
    Task ConsumeAsync(CancellationToken cancellationToken);
    void Dispose();
}
