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
                retryCount: _config.MaxTentativas,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromMilliseconds(_config.DelayEntreTentativasMs * Math.Pow(2, retryAttempt)),
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
            _logger.LogInformation("📥 Mensagem Kafka recebida: {Json}", mensagemJson);

            // 🔥 CORREÇÃO CRÍTICA: Configurar desserialização correta
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Importante para aceitar maiúsculas/minúsculas
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            // 1. Desserializar evento
            var evento = JsonSerializer.Deserialize<TransferenciaRealizadaEvent>(mensagemJson, options);
            if (evento == null)
            {
                _logger.LogError("Falha ao desserializar mensagem do Kafka");
                return false;
            }

            _logger.LogInformation("✅ Dados parseados: ID={Id}, Valor={Valor}, Origem={Origem}",
                evento.TransferenciaId, evento.Valor, evento.ContaOrigemId);

            // 2. Verificar duplicidade
            var jaProcessada = await _tarifaRepository.TransferenciaJaProcessadaAsync(
                evento.TransferenciaId, topico, offset, cancellationToken);

            if (jaProcessada)
            {
                _logger.LogInformation("Transferência já processada: {TransferenciaId}", evento.TransferenciaId);
                return true; // Considera como sucesso para não reprocessar
            }

            // 3. 🔥 CALCULAR TARIFA (agora é responsabilidade do worker)
            decimal valorTarifa = CalcularTarifa(evento.Valor);

            _logger.LogInformation("💰 Calculando tarifa: R$ {Tarifa} para transferência {Id} de R$ {Valor}",
                valorTarifa, evento.TransferenciaId, evento.Valor);

            // 4. Aplicar tarifa com retry
            var tarifaAplicada = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _contaCorrenteClient.AplicarTarifaAsync(
                    evento.ContaOrigemId,
                    evento.TransferenciaId,
                    valorTarifa,
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
                    Valor = valorTarifa,
                    Processada = true,
                    DataProcessamento = DateTime.UtcNow
                };

                await _tarifaRepository.InserirTarifaAsync(tarifa, cancellationToken);

                // 6. Registrar histórico de processamento com sucesso
                await RegistrarProcessamentoComSucesso(
                    evento, valorTarifa, topico, offset, "SUCESSO", cancellationToken);

                // 7. Publicar no tópico de tarifas processadas
                await PublicarTarifaProcessada(evento, valorTarifa, cancellationToken);

                _logger.LogInformation("✅ Tarifa processada com sucesso: Transferencia={TransferenciaId}, Valor={Valor}",
                    evento.TransferenciaId, valorTarifa);

                return true;
            }
            else
            {
                // 8. Registrar falha
                await RegistrarProcessamentoComFalha(
                    evento, valorTarifa, topico, offset, "FALHA_APLICACAO_TARIFA", cancellationToken);

                _logger.LogError("❌ Falha ao aplicar tarifa: Transferencia={TransferenciaId}", evento.TransferenciaId);

                return false; // Não commit no offset para reprocessar
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ Erro de JSON na mensagem: {Mensagem}", mensagemJson);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro inesperado no processamento da mensagem");
            return false;
        }
    }

    private decimal CalcularTarifa(decimal valorTransferencia)
    {
        // 🔥 Retorna o valor fixo configurado (R$ 2,00)
        return _config.ValorTarifaMinima;
    }

    private async Task RegistrarProcessamentoComSucesso(
        TransferenciaRealizadaEvent evento,
        decimal valorTarifaAplicada,
        string topico,
        long offset,
        string status,
        CancellationToken cancellationToken)
    {
        var historico = new TarifaProcessadaEntity
        {
            TransferenciaId = evento.TransferenciaId,
            ContaOrigemId = evento.ContaOrigemId,
            ValorTarifa = valorTarifaAplicada,
            DataProcessamento = DateTime.UtcNow,
            Status = status,
            Mensagem = $"Tarifa aplicada: R$ {valorTarifaAplicada}",
            TopicoKafka = topico,
            OffsetKafka = offset
        };

        await _tarifaRepository.RegistrarProcessamentoAsync(historico, cancellationToken);
        
        _logger.LogDebug("📝 Histórico registrado: Transferencia={Id}, Status={Status}", 
            evento.TransferenciaId, status);
    }

    private async Task RegistrarProcessamentoComFalha(
        TransferenciaRealizadaEvent evento,
        decimal valorTarifaTentada,
        string topico,
        long offset,
        string mensagemErro,
        CancellationToken cancellationToken)
    {
        var historico = new TarifaProcessadaEntity
        {
            TransferenciaId = evento.TransferenciaId,
            ContaOrigemId = evento.ContaOrigemId,
            ValorTarifa = valorTarifaTentada,
            DataProcessamento = DateTime.UtcNow,
            Status = "FALHA",
            Mensagem = mensagemErro,
            TopicoKafka = topico,
            OffsetKafka = offset
        };

        await _tarifaRepository.RegistrarProcessamentoAsync(historico, cancellationToken);
        
        _logger.LogDebug("📝 Histórico de falha registrado: Transferencia={Id}", evento.TransferenciaId);
    }

    private async Task PublicarTarifaProcessada(
        TransferenciaRealizadaEvent evento,
        decimal valorTarifa,
        CancellationToken cancellationToken)
    {
        try
        {
            var mensagem = new
            {
                tarifaId = Guid.NewGuid(),
                transferenciaId = evento.TransferenciaId,
                contaId = evento.ContaOrigemId,
                valorTransferencia = evento.Valor,
                valorTarifa = valorTarifa,
                dataTransferencia = evento.DataMovimento,
                dataProcessamento = DateTime.UtcNow,
                identificacaoRequisicao = evento.IdentificacaoRequisicao ?? "N/A"
            };

            await _kafkaProducerService.ProduzirMensagemAsync(
                "tarifas-processadas",
                evento.TransferenciaId.ToString(),
                mensagem,
                cancellationToken);

            _logger.LogDebug("📤 Tarifa publicada no tópico 'tarifas-processadas': {TransferenciaId}",
                evento.TransferenciaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Erro ao publicar tarifa processada no Kafka");
            // Não falha o processamento principal
        }
    }
}

public class TarifaConfig
{
    public decimal ValorTarifaMinima { get; set; } = 2.00m;
    public int MaxTentativas { get; set; } = 3;
    public int DelayEntreTentativasMs { get; set; } = 1000;
}
