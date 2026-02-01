using Ailos.Tarifa.Worker.Infrastructure.Kafka;

namespace Ailos.Tarifa.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IKafkaConsumerService _kafkaConsumerService;
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public Worker(
        IKafkaConsumerService kafkaConsumerService,
        ILogger<Worker> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _kafkaConsumerService = kafkaConsumerService;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de Tarifas iniciado em: {Time}", DateTimeOffset.Now);

        try
        {
            await _kafkaConsumerService.ConsumeAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker de Tarifas cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Erro fatal no Worker de Tarifas");
            _applicationLifetime.StopApplication();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker de Tarifas parando...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Worker de Tarifas parado");
    }
}
