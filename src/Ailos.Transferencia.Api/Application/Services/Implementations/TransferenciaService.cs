using System.Text.Json;
using Ailos.Transferencia.Api.Application.DTOs.Transferencia;
using Ailos.Transferencia.Api.Domain.Entities;
using Ailos.Transferencia.Api.Infrastructure.Clients;
using Ailos.Transferencia.Api.Infrastructure.Kafka;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.EncryptedId;
using Microsoft.Extensions.Options;
using Ailos.Common.Domain.Exceptions;

namespace Ailos.Transferencia.Api.Application.Services;

public sealed class TransferenciaService : ITransferenciaService
{
    private readonly ITransferenciaRepository _transferenciaRepository;
    private readonly IIdempotenciaService _idempotenciaService;
    private readonly IContaCorrenteClient _contaCorrenteClient;
    private readonly IEncryptedIdService _encryptedIdService;
    private readonly IKafkaProducerService _kafkaProducerService;
    private readonly TarifaConfig _tarifaConfig;

    public TransferenciaService(
        ITransferenciaRepository transferenciaRepository,
        IIdempotenciaService idempotenciaService,
        IContaCorrenteClient contaCorrenteClient,
        IEncryptedIdService encryptedIdService,
        IKafkaProducerService kafkaProducerService,
        IOptions<TarifaConfig> tarifaConfig)
    {
        _transferenciaRepository = transferenciaRepository;
        _idempotenciaService = idempotenciaService;
        _contaCorrenteClient = contaCorrenteClient;
        _encryptedIdService = encryptedIdService;
        _kafkaProducerService = kafkaProducerService;
        _tarifaConfig = tarifaConfig.Value;
    }

    public async Task<TransferenciaResponse> CriarTransferenciaAsync(
        long contaIdUsuarioLogado,
        TransferenciaRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Verificar idempotência
        if (await _idempotenciaService.RequisicaoJaProcessadaAsync(request.IdentificacaoRequisicao, cancellationToken))
        {
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

            // 4. Validar transferência
            ValidarTransferencia(contaIdUsuarioLogado, contaDestinoId, request.Valor);

            // 5. Criar entidade de transferência
            var transferencia = new TransferenciaEntity(
                contaIdUsuarioLogado,
                contaDestinoId,
                request.Valor,
                request.IdentificacaoRequisicao);

            // 6. Aplicar tarifa se configurada
            if (_tarifaConfig.ValorTarifa > 0)
            {
                transferencia.AplicarTarifa(_tarifaConfig.ValorTarifa);
            }

            // 7. Salvar transferência inicial
            var transferenciaSalva = await _transferenciaRepository.InserirAsync(transferencia, cancellationToken);

            try
            {
                // 8. Realizar débito na conta de origem
                await _contaCorrenteClient.RealizarMovimentacaoAsync(
                    contaIdUsuarioLogado,
                    "D",
                    request.Valor,
                    $"Transferência para conta {contaDestinoId}",
                    request.IdentificacaoRequisicao,
                    cancellationToken);

                // 9. Realizar crédito na conta de destino
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

                return response;
            }
            catch (Exception ex)
            {
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
        return await _transferenciaRepository.ObterPorContaAsync(contaId, cancellationToken);
    }

    // Métodos privados auxiliares
    private void ValidarTransferencia(long contaOrigemId, long contaDestinoId, decimal valor)
    {
        if (contaOrigemId == contaDestinoId)
            throw new ValidationException("Conta de origem e destino não podem ser iguais");

        if (valor <= 0)
            throw new ValidationException("Valor deve ser positivo");
    }

    private async Task<TransferenciaResponse> ProcessarRequisicaoIdempotente(
        string identificacaoRequisicao,
        CancellationToken cancellationToken)
    {
        var resultadoAnterior = await _idempotenciaService.ObterResultadoAsync(
            identificacaoRequisicao, cancellationToken);

        if (!string.IsNullOrEmpty(resultadoAnterior))
        {
            var resultado = JsonSerializer.Deserialize<ResultadoIdempotencia>(resultadoAnterior);

            if (resultado?.Erro != null)
            {
                throw new InvalidOperationException($"Requisição anterior falhou: {resultado.Erro}");
            }

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

        throw new InvalidOperationException("Requisição idempotente sem resultado");
    }

    private async Task RealizarEstorno(long contaId, decimal valor, CancellationToken cancellationToken)
    {
        try
        {
            await _contaCorrenteClient.RealizarMovimentacaoAsync(
                contaId,
                "C",
                valor,
                "Estorno de transferência",
                Guid.NewGuid().ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Log do erro de estorno, mas não propagar
            Console.WriteLine($"Erro ao realizar estorno: {ex.Message}");
        }
    }

    private async Task PublicarTransferenciaNoKafka(
        TransferenciaEntity transferencia,
        CancellationToken cancellationToken)
    {
        var mensagem = new TransferenciaKafkaMessage
        {
            TransferenciaId = transferencia.Id,
            ContaOrigemId = transferencia.ContaCorrenteOrigemId,
            ContaDestinoId = transferencia.ContaCorrenteDestinoId,
            Valor = transferencia.Valor,
            TarifaAplicada = transferencia.TarifaAplicada ?? 0,
            DataMovimento = transferencia.DataMovimento
        };

        await _kafkaProducerService.ProduzirMensagemAsync(
            "transferencias-realizadas",
            transferencia.Id.ToString(),
            mensagem,
            cancellationToken);
    }

    private TransferenciaResponse CriarResponse(
        TransferenciaEntity transferencia,
        long contaOrigemId,
        long contaDestinoId)
    {
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
    public decimal ValorTarifa { get; set; } = 2.00m;
}
