using System.Text.Json;
using Ailos.Tarifa.Worker.Domain.Events;
using Ailos.Tarifa.Worker.Domain.Entities;
using Ailos.Tarifa.Worker.Infrastructure.Clients;
using Ailos.Tarifa.Worker.Infrastructure.Kafka;
using Ailos.Tarifa.Worker.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Polly;
using Microsoft.Extensions.Logging;

namespace Ailos.Tarifa.Worker.Application.Services;

public sealed class TarifaProcessor : ITarifaProcessor
{
    private readonly ITarifaRepository _tarifaRepository;
    private readonly IContaCorrenteClient _contaCorrenteClient;
    private readonly IKafkaProducerService _kafkaProducerService;
    private readonly ILogger<TarifaProcessor> _logger;
    private readonly TarifaConfig _config;
    private readonly IAsyncPolicy _retryPolicy;

    public TarifaProcessor(
        ITarifaRepository tarifaRepository,
        IContaCorrenteClient contaCorrenteClient,
        IKafkaProducerService kafkaProducerService,
        ILogger<TarifaProcessor> logger,
        IOptions<TarifaConfig> config)
    {
        _tarifaRepository = tarifaRepository;
        _contaCorrenteClient = contaCorrenteClient;
        _kafkaProducerService = kafkaProducerService;
        _logger = logger;
        _config = config.Value;
        
        // Configurar política de retry com exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, 
                        "Tentativa {RetryCount} falhou. Aguardando {TimeSpan} antes da próxima tentativa.", 
                        retryCount, timeSpan);
                });
    }

    public async Task<bool> ProcessarMensagemAsync(
        string mensagemJson, 
        string topico, 
        int partition, 
        long offset, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processando mensagem: {Json}", mensagemJson);

            // 1. Desserializar evento
            var evento = JsonSerializer.Deserialize<TransferenciaRealizadaEvent>(mensagemJson);
            if (evento == null)
            {
                _logger.LogError("Falha ao desserializar mensagem do Kafka");
                return false;
            }

            // 2. Verificar duplicidade
            var jaProcessada = await _tarifaRepository.TransferenciaJaProcessadaAsync(
                evento.TransferenciaId, topico, offset, cancellationToken);
            
            if (jaProcessada)
            {
                _logger.LogInformation("Transferência já processada: {TransferenciaId}", evento.TransferenciaId);
                return true; // Considera como sucesso para não reprocessar
            }

            // 3. Validar tarifa
            if (evento.TarifaAplicada <= 0)
            {
                _logger.LogInformation("Transferência sem tarifa: {TransferenciaId}", evento.TransferenciaId);
                
                await RegistrarProcessamentoComSucesso(
                    evento, topico, partition, offset, "SEM_TARIFA", cancellationToken);
                
                return true;
            }

            // 4. Aplicar tarifa com retry
            var tarifaAplicada = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _contaCorrenteClient.AplicarTarifaAsync(
                    evento.ContaOrigemId,
                    evento.TransferenciaId,
                    evento.TarifaAplicada,
                    cancellationToken);
            });

            if (tarifaAplicada)
            {
                // 5. Registrar tarifa no banco
                var tarifa = new TarifaEntity
                {
                    ContaCorrenteId = evento.ContaOrigemId,
                    TransferenciaId = evento.TransferenciaId,
                    DataMovimento = evento.DataMovimento,
                    Valor = evento.TarifaAplicada,
                    Processada = true,
                    DataProcessamento = DateTime.UtcNow
                };

                await _tarifaRepository.InserirTarifaAsync(tarifa, cancellationToken);

                // 6. Registrar histórico de processamento
                await RegistrarProcessamentoComSucesso(
                    evento, topico, partition, offset, "SUCESSO", cancellationToken);

                // 7. Publicar no tópico de tarifas processadas
                await PublicarTarifaProcessada(evento, cancellationToken);

                _logger.LogInformation("Tarifa processada com sucesso: Transferencia={TransferenciaId}, Valor={Valor}", 
                    evento.TransferenciaId, evento.TarifaAplicada);
                
                return true;
            }
            else
            {
                // 8. Registrar falha
                await RegistrarProcessamentoComFalha(
                    evento, topico, partition, offset, "FALHA_APLICACAO_TARIFA", cancellationToken);
                
                _logger.LogError("Falha ao aplicar tarifa: Transferencia={TransferenciaId}", evento.TransferenciaId);
                
                return false; // Não commit no offset para reprocessar
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro de JSON na mensagem: {Mensagem}", mensagemJson);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no processamento da mensagem");
            return false;
        }
    }

    private async Task RegistrarProcessamentoComSucesso(
        TransferenciaRealizadaEvent evento,
        string topico,
        int partition,
        long offset,
        string status,
        CancellationToken cancellationToken)
    {
        var historico = new TarifaProcessadaEntity
        {
            TransferenciaId = evento.TransferenciaId,
            ContaOrigemId = evento.ContaOrigemId,
            ValorTarifa = evento.TarifaAplicada,
            DataProcessamento = DateTime.UtcNow,
            Status = status,
            TopicoKafka = topico,
            OffsetKafka = offset
        };

        await _tarifaRepository.RegistrarProcessamentoAsync(historico, cancellationToken);
    }

    private async Task RegistrarProcessamentoComFalha(
        TransferenciaRealizadaEvent evento,
        string topico,
        int partition,
        long offset,
        string mensagemErro,
        CancellationToken cancellationToken)
    {
        var historico = new TarifaProcessadaEntity
        {
            TransferenciaId = evento.TransferenciaId,
            ContaOrigemId = evento.ContaOrigemId,
            ValorTarifa = evento.TarifaAplicada,
            DataProcessamento = DateTime.UtcNow,
            Status = "FALHA",
            Mensagem = mensagemErro,
            TopicoKafka = topico,
            OffsetKafka = offset
        };

        await _tarifaRepository.RegistrarProcessamentoAsync(historico, cancellationToken);
    }

    private async Task PublicarTarifaProcessada(
        TransferenciaRealizadaEvent evento,
        CancellationToken cancellationToken)
    {
        try
        {
            var mensagem = new
            {
                tarifaId = Guid.NewGuid(),
                transferenciaId = evento.TransferenciaId,
                contaId = evento.ContaOrigemId,
                valorTarifa = evento.TarifaAplicada,
                dataProcessamento = DateTime.UtcNow
            };

            await _kafkaProducerService.ProduzirMensagemAsync(
                "tarifas-processadas",
                evento.TransferenciaId.ToString(),
                mensagem,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar tarifa processada no Kafka");
            // Não falha o processamento principal se não conseguir publicar
        }
    }
}

public class TarifaConfig
{
    public decimal ValorTarifaMinima { get; set; } = 0.01m;
    public int MaxTentativas { get; set; } = 3;
    public int DelayEntreTentativasMs { get; set; } = 1000;
}
