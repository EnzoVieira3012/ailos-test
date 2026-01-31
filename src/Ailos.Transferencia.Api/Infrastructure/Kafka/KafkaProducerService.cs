using Confluent.Kafka;
using System.Text.Json;

namespace Ailos.Transferencia.Api.Infrastructure.Kafka;

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
    private readonly KafkaConfig _config;

    public KafkaProducerService(KafkaConfig config)
    {
        _config = config;
        
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };
        
        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(Serializers.Utf8)
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
                throw new InvalidOperationException($"Falha ao persistir mensagem no Kafka: {result.Status}");
            }
        }
        catch (ProduceException<string, string> ex)
        {
            throw new InvalidOperationException($"Erro ao produzir mensagem no Kafka: {ex.Error.Reason}", ex);
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}

public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string TransferenciasTopic { get; set; } = "transferencias-realizadas";
    public string TarifasTopic { get; set; } = "tarifas-realizadas";
}
