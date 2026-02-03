namespace Ailos.Common.Messaging.Implementations;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default);
}
