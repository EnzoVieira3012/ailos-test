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
        try
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
            {
                _logger.LogWarning("Kafka não persistiu mensagem. Status: {Status}", result.Status);
            }

            _logger.LogInformation(
                "📤 Kafka | Topic: {Topic} | Key: {Key} | Offset: {Offset}",
                topic, key, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar mensagem no Kafka");
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
            _logger.LogInformation("🛑 Kafka Producer finalizado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar Kafka Producer");
        }
    }
}