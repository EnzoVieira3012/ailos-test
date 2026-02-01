using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Ailos.Common.Configuration;

namespace Ailos.Common.Messaging;

public sealed class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        KafkaConnectionFactory factory,
        ILogger<KafkaProducerService> logger)
    {
        _producer = factory.CreateProducer();
        _logger = logger;

        _logger.LogInformation("✅ Kafka Producer inicializado via Common");
    }

    public async Task PublishAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message);

        var result = await _producer.ProduceAsync(
            topic,
            new Message<string, string>
            {
                Key = key,
                Value = payload,
                Timestamp = Timestamp.Default
            },
            cancellationToken
        );

        if (result.Status != PersistenceStatus.Persisted)
            throw new InvalidOperationException(
                $"Kafka não persistiu mensagem. Status: {result.Status}");

        _logger.LogInformation(
            "📤 Kafka | Topic: {Topic} | Offset: {Offset}",
            topic,
            result.Offset.Value);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        _logger.LogInformation("🛑 Kafka Producer finalizado");
    }
}

