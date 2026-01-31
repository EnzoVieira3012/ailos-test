using Confluent.Kafka;
using System.Text.Json;

namespace Ailos.Tarifa.Worker.Infrastructure.Kafka;

public interface IKafkaProducerService
{
    Task ProduzirMensagemAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default);
}

public sealed class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        KafkaConfig config,
        ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            LingerMs = 5
        };
        
        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(Serializers.Utf8)
            .SetErrorHandler((_, error) =>
                _logger.LogError("Erro no Kafka Producer: {Reason}", error.Reason))
            .Build();
    }

    public async Task ProduzirMensagemAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageJson = JsonSerializer.Serialize(message);
            
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = messageJson,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };
            
            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            
            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogWarning("Mensagem não persistida: Status={Status}", result.Status);
            }
            else
            {
                _logger.LogDebug("Mensagem publicada: Topic={Topic}, Partition={Partition}, Offset={Offset}",
                    topic, result.Partition, result.Offset);
            }
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Erro ao produzir mensagem no Kafka: {Error}", ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(5));
        _producer?.Dispose();
        _logger.LogInformation("Kafka Producer finalizado");
    }
}
