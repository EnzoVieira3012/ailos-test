namespace Ailos.Common.Messaging;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default);
}
