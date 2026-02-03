using System.Text.Json;
using Ailos.Transferencia.Api.Application.DTOs.Transferencia;
using Ailos.Transferencia.Api.Domain.Entities;
using Ailos.Transferencia.Api.Infrastructure.Clients;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.EncryptedId;
using Microsoft.Extensions.Options;
using Ailos.Common.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Ailos.Common.Messaging;

namespace Ailos.Transferencia.Api.Application.Services;

public sealed class TransferenciaService : ITransferenciaService
{
    private readonly ITransferenciaRepository _transferenciaRepository;
    private readonly IIdempotenciaService _idempotenciaService;
    private readonly IContaCorrenteClient _contaCorrenteClient;
    private readonly IEncryptedIdService _encryptedIdService;
    private readonly IKafkaProducerService _kafkaProducerService;
    private readonly TarifaConfig _tarifaConfig;
    private readonly ILogger<TransferenciaService> _logger;

    public TransferenciaService(
        ITransferenciaRepository transferenciaRepository,
        IIdempotenciaService idempotenciaService,
        IContaCorrenteClient contaCorrenteClient,
        IEncryptedIdService encryptedIdService,
        IKafkaProducerService kafkaProducerService,
        IOptions<TarifaConfig> tarifaConfig,
        ILogger<TransferenciaService> logger)
    {
        _transferenciaRepository = transferenciaRepository;
        _idempotenciaService = idempotenciaService;
        _contaCorrenteClient = contaCorrenteClient;
        _encryptedIdService = encryptedIdService;
        _kafkaProducerService = kafkaProducerService;
        _tarifaConfig = tarifaConfig.Value;
        _logger = logger;
    }

    public async Task<TransferenciaResponse> CriarTransferenciaAsync(
        long contaIdUsuarioLogado,
        TransferenciaRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando transferência para conta {ContaOrigem}", contaIdUsuarioLogado);

        // 1. Verificar idempotência
        if (await _idempotenciaService.RequisicaoJaProcessadaAsync(request.IdentificacaoRequisicao, cancellationToken))
        {
            _logger.LogWarning("Requisição idempotente detectada: {Identificacao}", request.IdentificacaoRequisicao);
            return await ProcessarRequisicaoIdempotente(request.IdentificacaoRequisicao, cancellationToken);
        }

        // 2. Registrar início do processamento
        await _idempotenciaService.RegistrarAsync(
            request.IdentificacaoRequisicao,
            null,
            null,
            cancellationToken);

        try
        {
            // 3. Descriptografar IDs
            var contaDestinoId = _encryptedIdService.Decrypt(request.ContaDestinoId);

            _logger.LogDebug("Conta destino decriptada: {ContaDestinoId}", contaDestinoId);

            // 4. Validar transferência
            ValidarTransferencia(contaIdUsuarioLogado, contaDestinoId, request.Valor);

            // 5. Criar entidade de transferência
            var transferencia = new TransferenciaEntity(
                contaIdUsuarioLogado,
                contaDestinoId,
                request.Valor,
                request.IdentificacaoRequisicao);

            // 6. NÃO APLICAR TARIFA AQUI - será aplicada pelo Tarifa Worker
            //    Mantemos o campo TarifaAplicada como 0 ou null
            transferencia.TarifaAplicada = 0;
            _logger.LogDebug("Tarifa não aplicada na API de Transferência. Será processada pelo Tarifa Worker.");

            // 7. Salvar transferência inicial
            var transferenciaSalva = await _transferenciaRepository.InserirAsync(transferencia, cancellationToken);
            _logger.LogInformation("Transferência salva no banco: ID {TransferenciaId}", transferenciaSalva.Id);

            try
            {
                // 8. Realizar débito na conta de origem
                _logger.LogDebug("Realizando débito na conta de origem {ContaOrigem}", contaIdUsuarioLogado);
                await _contaCorrenteClient.RealizarMovimentacaoAsync(
                    contaIdUsuarioLogado,
                    "D",
                    request.Valor,
                    $"Transferência para conta {contaDestinoId}",
                    request.IdentificacaoRequisicao,
                    cancellationToken);

                // 9. Realizar crédito na conta de destino
                _logger.LogDebug("Realizando crédito na conta de destino {ContaDestino}", contaDestinoId);
                await _contaCorrenteClient.RealizarMovimentacaoAsync(
                    contaDestinoId,
                    "C",
                    request.Valor,
                    $"Transferência recebida de conta {contaIdUsuarioLogado}",
                    request.IdentificacaoRequisicao,
                    cancellationToken);

                // 10. Atualizar status da transferência para concluída
                transferenciaSalva.Concluir();
                await _transferenciaRepository.AtualizarAsync(transferenciaSalva, cancellationToken);

                // 11. Publicar no Kafka para tarifação
                await PublicarTransferenciaNoKafka(transferenciaSalva, cancellationToken);

                // 12. Preparar resposta
                var response = CriarResponse(transferenciaSalva, contaIdUsuarioLogado, contaDestinoId);

                // 13. Registrar sucesso na idempotência
                await _idempotenciaService.RegistrarAsync(
                    request.IdentificacaoRequisicao,
                    null,
                    SerializarResultado(response),
                    cancellationToken);

                _logger.LogInformation("Transferência concluída com sucesso: {TransferenciaId}", transferenciaSalva.Id);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante processamento da transferência. Realizando estorno...");

                // 14. Em caso de erro, fazer estorno
                await RealizarEstorno(contaIdUsuarioLogado, request.Valor, cancellationToken);

                // 15. Atualizar status da transferência para falha
                transferenciaSalva.Falhar(ex.Message);
                await _transferenciaRepository.AtualizarAsync(transferenciaSalva, cancellationToken);

                // 16. Registrar falha na idempotência
                await _idempotenciaService.RegistrarAsync(
                    request.IdentificacaoRequisicao,
                    null,
                    SerializarErro(ex),
                    cancellationToken);

                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inicial na transferência");

            // Registrar falha inicial na idempotência
            await _idempotenciaService.RegistrarAsync(
                request.IdentificacaoRequisicao,
                null,
                SerializarErro(ex),
                cancellationToken);

            throw;
        }
    }

    public async Task<IEnumerable<TransferenciaEntity>> ObterTransferenciasPorContaAsync(
        long contaId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Obtendo transferências para conta {ContaId}", contaId);

        var transferencias = await _transferenciaRepository.ObterPorContaAsync(contaId, cancellationToken);

        _logger.LogInformation("Retornadas {Quantidade} transferências para conta {ContaId}",
            transferencias.Count(), contaId);

        return transferencias;
    }

    // Métodos privados auxiliares
    private void ValidarTransferencia(long contaOrigemId, long contaDestinoId, decimal valor)
    {
        if (contaOrigemId == contaDestinoId)
        {
            _logger.LogWarning("Conta de origem e destino iguais: {ContaId}", contaOrigemId);
            throw new ValidationException("Conta de origem e destino não podem ser iguais");
        }

        if (valor <= 0)
        {
            _logger.LogWarning("Valor inválido: {Valor}", valor);
            throw new ValidationException("Valor deve ser positivo");
        }

        if (valor > 1000000) // Limite máximo de transferência
        {
            _logger.LogWarning("Valor excede limite máximo: {Valor}", valor);
            throw new ValidationException("Valor máximo para transferência é R$ 1.000.000,00");
        }

        _logger.LogDebug("Validação de transferência aprovada: Origem={ContaOrigem}, Destino={ContaDestino}, Valor={Valor}",
            contaOrigemId, contaDestinoId, valor);
    }

    private async Task<TransferenciaResponse> ProcessarRequisicaoIdempotente(
        string identificacaoRequisicao,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processando requisição idempotente: {Identificacao}", identificacaoRequisicao);

        var resultadoAnterior = await _idempotenciaService.ObterResultadoAsync(
            identificacaoRequisicao, cancellationToken);

        if (!string.IsNullOrEmpty(resultadoAnterior))
        {
            var resultado = JsonSerializer.Deserialize<ResultadoIdempotencia>(resultadoAnterior);

            if (resultado?.Erro != null)
            {
                _logger.LogWarning("Requisição anterior falhou: {Erro}", resultado.Erro);
                throw new InvalidOperationException($"Requisição anterior falhou: {resultado.Erro}");
            }

            _logger.LogInformation("Retornando resultado idempotente da transferência {TransferenciaId}",
                resultado?.TransferenciaId);

            return new TransferenciaResponse
            {
                TransferenciaId = new Ailos.EncryptedId.EncryptedId(resultado?.TransferenciaId ?? string.Empty),
                ContaOrigemId = new Ailos.EncryptedId.EncryptedId(resultado?.ContaOrigemId ?? string.Empty),
                ContaDestinoId = new Ailos.EncryptedId.EncryptedId(resultado?.ContaDestinoId ?? string.Empty),
                Valor = resultado?.Valor ?? 0,
                TarifaAplicada = resultado?.TarifaAplicada,
                DataMovimento = resultado?.DataProcessamento ?? DateTime.UtcNow,
                Status = resultado?.Status ?? "CONCLUIDA"
            };
        }

        _logger.LogError("Requisição idempotente sem resultado: {Identificacao}", identificacaoRequisicao);
        throw new InvalidOperationException("Requisição idempotente sem resultado");
    }

    private async Task RealizarEstorno(long contaId, decimal valor, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning("Realizando estorno para conta {ContaId} no valor de R$ {Valor}", contaId, valor);

            await _contaCorrenteClient.RealizarMovimentacaoAsync(
                contaId,
                "C",
                valor,
                "Estorno de transferência",
                Guid.NewGuid().ToString(),
                cancellationToken);

            _logger.LogInformation("Estorno realizado com sucesso para conta {ContaId}", contaId);
        }
        catch (Exception ex)
        {
            // Log do erro de estorno, mas não propagar para não interromper o fluxo principal
            _logger.LogCritical(ex, "CRÍTICO: Falha ao realizar estorno para conta {ContaId}. Valor não estornado: R$ {Valor}",
                contaId, valor);

            // Aqui poderíamos enviar uma notificação para a equipe de suporte
            // ou registrar em um sistema de monitoramento
        }
    }

    // No método PublicarTransferenciaNoKafka da Transferência API
    private async Task PublicarTransferenciaNoKafka(
        TransferenciaEntity transferencia,
        CancellationToken cancellationToken)
    {
        try
        {
            var mensagem = new
            {
                TransferenciaId = transferencia.Id,
                ContaOrigemId = transferencia.ContaCorrenteOrigemId,
                ContaDestinoId = transferencia.ContaCorrenteDestinoId,
                Valor = transferencia.Valor,
                TarifaAplicada = 0, // SEM tarifa - o worker vai calcular
                DataMovimento = transferencia.DataMovimento,
                IdentificacaoRequisicao = transferencia.IdentificacaoRequisicao
            };

            _logger.LogDebug("📤 Publicando transferência no Kafka: {TransferenciaId}", transferencia.Id);

            await _kafkaProducerService.PublishAsync(
                "transferencias-realizadas",
                transferencia.Id.ToString(),
                mensagem,
                cancellationToken);

            _logger.LogInformation("✅ Transferência publicada no Kafka com sucesso: {TransferenciaId}", transferencia.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar transferência no Kafka: {TransferenciaId}", transferencia.Id);
        }
    }

    private TransferenciaResponse CriarResponse(
        TransferenciaEntity transferencia,
        long contaOrigemId,
        long contaDestinoId)
    {
        _logger.LogDebug("Criando resposta para transferência {TransferenciaId}", transferencia.Id);

        return new TransferenciaResponse
        {
            TransferenciaId = _encryptedIdService.Encrypt(transferencia.Id),
            ContaOrigemId = _encryptedIdService.Encrypt(contaOrigemId),
            ContaDestinoId = _encryptedIdService.Encrypt(contaDestinoId),
            Valor = transferencia.Valor,
            TarifaAplicada = transferencia.TarifaAplicada,
            DataMovimento = transferencia.DataMovimento,
            Status = transferencia.Status.ToString()
        };
    }

    private string SerializarResultado(TransferenciaResponse response)
    {
        return JsonSerializer.Serialize(new ResultadoIdempotencia
        {
            TransferenciaId = response.TransferenciaId.Value,
            ContaOrigemId = response.ContaOrigemId.Value,
            ContaDestinoId = response.ContaDestinoId.Value,
            Valor = response.Valor,
            TarifaAplicada = response.TarifaAplicada,
            Status = response.Status,
            DataProcessamento = DateTime.UtcNow
        });
    }

    private string SerializarErro(Exception ex)
    {
        var errorType = ex is DomainException domainEx ? domainEx.ErrorCode : "INTERNAL_ERROR";

        _logger.LogDebug("Serializando erro: {ErrorType} - {Message}", errorType, ex.Message);

        return JsonSerializer.Serialize(new ResultadoIdempotencia
        {
            Erro = ex.Message,
            TipoErro = errorType,
            DataProcessamento = DateTime.UtcNow
        });
    }

    // Classes internas para serialização
    private class ResultadoIdempotencia
    {
        public string? TransferenciaId { get; set; }
        public string? ContaOrigemId { get; set; }
        public string? ContaDestinoId { get; set; }
        public decimal? Valor { get; set; }
        public decimal? TarifaAplicada { get; set; }
        public string? Status { get; set; }
        public string? Erro { get; set; }
        public string? TipoErro { get; set; }
        public DateTime DataProcessamento { get; set; }
    }

    private class TransferenciaKafkaMessage
    {
        public long TransferenciaId { get; set; }
        public long ContaOrigemId { get; set; }
        public long ContaDestinoId { get; set; }
        public decimal Valor { get; set; }
        public decimal TarifaAplicada { get; set; }
        public DateTime DataMovimento { get; set; }
    }
}

public class TarifaConfig
{
    // 🔥 Agora esta configuração é usada apenas para manter compatibilidade
    //    A tarifa real será aplicada pelo Tarifa Worker
    public decimal ValorTarifa { get; set; } = 0; // Definir como 0
}
