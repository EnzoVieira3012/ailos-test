using Confluent.Kafka;
using System.Text.Json;
using Ailos.Common.Configuration;

namespace Ailos.Tarifa.Worker.Application.Services.Implementations;

public sealed class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string>? _producer;
    private readonly ILogger<KafkaProducerService> _logger;
    private readonly KafkaSettings _settings;
    private bool _disposed;

    public KafkaProducerService(
        KafkaSettings settings,
        ILogger<KafkaProducerService> logger)
    {
        _settings = settings;
        _logger = logger;

        try
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
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
                
            _logger.LogInformation("Kafka Producer criado para servidor: {BootstrapServers}", _settings.BootstrapServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar Kafka Producer");
            _producer = null;
        }
    }

    public async Task ProduzirMensagemAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default)
    {
        if (_producer == null)
        {
            _logger.LogError("Kafka Producer não inicializado");
            return;
        }

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
        if (_disposed) return;
        
        try
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
            _logger.LogInformation("Kafka Producer finalizado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar Kafka Producer");
        }
        finally
        {
            _disposed = true;
        }
    }
}