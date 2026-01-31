using Confluent.Kafka;

namespace Ailos.Tarifa.Worker.Infrastructure.Kafka;

public interface IKafkaConsumerService
{
    Task ConsumeAsync(CancellationToken cancellationToken);
    void Dispose();
}

public sealed class KafkaConsumerService : IKafkaConsumerService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly Application.Services.ITarifaProcessor _tarifaProcessor;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly KafkaConfig _config;

    public KafkaConsumerService(
        KafkaConfig config,
        Application.Services.ITarifaProcessor tarifaProcessor,
        ILogger<KafkaConsumerService> logger)
    {
        _config = config;
        _tarifaProcessor = tarifaProcessor;
        _logger = logger;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config.BootstrapServers,
            GroupId = config.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, error) =>
                _logger.LogError("Erro no Kafka Consumer: {Reason}", error.Reason))
            .SetLogHandler((_, logMessage) =>
                _logger.LogDebug("Kafka Log: {Message} (Level: {Level})", 
                    logMessage.Message, logMessage.Level))
            .Build();
    }

    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_config.TransferenciasTopic);

        _logger.LogInformation("Iniciando consumo do tópico: {Topic}", _config.TransferenciasTopic);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);

                if (consumeResult?.Message?.Value == null)
                {
                    _logger.LogWarning("Mensagem nula recebida do Kafka");
                    continue;
                }

                _logger.LogDebug("Mensagem recebida - Tópico: {Topic}, Partição: {Partition}, Offset: {Offset}",
                    consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);

                // Processar a mensagem
                var processado = await _tarifaProcessor.ProcessarMensagemAsync(
                    consumeResult.Message.Value,
                    consumeResult.Topic,
                    consumeResult.Partition.Value,
                    consumeResult.Offset.Value,
                    cancellationToken);

                if (processado)
                {
                    // Commit manual do offset
                    _consumer.StoreOffset(consumeResult);
                    _consumer.Commit();
                    
                    _logger.LogDebug("Offset commitado - Partition: {Partition}, Offset: {Offset}",
                        consumeResult.Partition, consumeResult.Offset);
                }
                else
                {
                    _logger.LogWarning("Mensagem não processada, mantendo offset");
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Erro ao consumir mensagem do Kafka");
                
                if (ex.Error.IsFatal)
                {
                    throw;
                }
                
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumo do Kafka cancelado");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado no consumo do Kafka");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        _logger.LogInformation("Kafka Consumer finalizado");
    }
}

public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string TransferenciasTopic { get; set; } = "transferencias-realizadas";
    public string TarifasTopic { get; set; } = "tarifas-processadas";
    public string ConsumerGroup { get; set; } = "tarifa-worker-group";
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}
