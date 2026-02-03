using Confluent.Kafka;
using Ailos.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Ailos.Tarifa.Worker.Infrastructure.Kafka;

public interface IKafkaConsumerService
{
    Task ConsumeAsync(CancellationToken cancellationToken);
    void Dispose();
}

public sealed class KafkaConsumerService : IKafkaConsumerService
{
    private readonly IConsumer<string, string>? _consumer;
    private readonly Application.Services.ITarifaProcessor _tarifaProcessor;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly KafkaSettings _settings;
    private bool _disposed;

    public KafkaConsumerService(
        KafkaSettings settings,
        Application.Services.ITarifaProcessor tarifaProcessor,
        ILogger<KafkaConsumerService> logger)
    {
        _settings = settings;
        _tarifaProcessor = tarifaProcessor;
        _logger = logger;

        try
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = _settings.ConsumerGroup,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false
            };

            _consumer = new ConsumerBuilder<string, string>(consumerConfig)
                .SetErrorHandler((_, error) =>
                    _logger.LogError("Erro no Kafka Consumer: {Reason}", error.Reason))
                .Build();
                
            _logger.LogInformation("Kafka Consumer criado para servidor: {BootstrapServers}", _settings.BootstrapServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar Kafka Consumer");
            _consumer = null;
        }
    }

    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        if (_consumer == null)
        {
            _logger.LogError("Kafka Consumer não inicializado");
            return;
        }

        _consumer.Subscribe(_settings.TransferenciasTopic);
        _logger.LogInformation("Iniciando consumo do tópico: {Topic}", _settings.TransferenciasTopic);

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
        if (_disposed) return;
        
        try
        {
            _consumer?.Close();
            _consumer?.Dispose();
            _logger.LogInformation("Kafka Consumer finalizado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar Kafka Consumer");
        }
        finally
        {
            _disposed = true;
        }
    }
}