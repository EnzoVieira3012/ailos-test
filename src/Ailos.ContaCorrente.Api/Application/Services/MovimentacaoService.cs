using Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Domain.Exceptions;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories;
using Ailos.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.Services;

public sealed class MovimentacaoService : IMovimentacaoService
{
    private readonly IContaCorrenteRepository _contaRepository;
    private readonly IMovimentoRepository _movimentoRepository;
    private readonly IIdempotenciaService _idempotenciaService;
    private readonly IEncryptedIdService _encryptedIdService;

    public MovimentacaoService(
        IContaCorrenteRepository contaRepository,
        IMovimentoRepository movimentoRepository,
        IIdempotenciaService idempotenciaService,
        IEncryptedIdService encryptedIdService)
    {
        _contaRepository = contaRepository;
        _movimentoRepository = movimentoRepository;
        _idempotenciaService = idempotenciaService;
        _encryptedIdService = encryptedIdService;
    }

    public async Task<MovimentacaoResponse> CriarMovimentacaoAsync(
        long contaIdUsuarioLogado,
        MovimentacaoRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Verificar idempotência ANTES de qualquer processamento
        if (await _idempotenciaService.RequisicaoJaProcessadaAsync(request.IdentificacaoRequisicao, cancellationToken))
        {
            var resultadoAnterior = await _idempotenciaService.ObterResultadoAsync(
                request.IdentificacaoRequisicao, cancellationToken);
            
            if (!string.IsNullOrEmpty(resultadoAnterior))
            {
                // Desserializar resultado anterior
                var resultado = System.Text.Json.JsonSerializer.Deserialize<ResultadoIdempotencia>(resultadoAnterior);
                
                if (resultado?.Erro != null)
                {
                    throw new InvalidOperationException($"Requisição anterior falhou: {resultado.Erro}");
                }
                
                return new MovimentacaoResponse
                {
                    MovimentoId = new Ailos.EncryptedId.EncryptedId(resultado?.MovimentoId ?? string.Empty),
                    DataMovimento = resultado?.DataProcessamento ?? DateTime.UtcNow,
                    SaldoAtual = resultado?.SaldoAtual ?? 0
                };
            }
        }

        // 2. Registrar início do processamento (sem resultado ainda)
        await _idempotenciaService.RegistrarAsync(
            request.IdentificacaoRequisicao,
            null,
            null,
            cancellationToken);

        try
        {
            // 3. Determinar conta destino
            long contaIdDestino = contaIdUsuarioLogado;
            if (request.ContaCorrenteId.HasValue)
            {
                contaIdDestino = _encryptedIdService.Decrypt(request.ContaCorrenteId.Value);
                
                // Validação específica
                if (contaIdDestino != contaIdUsuarioLogado && request.TipoMovimento != "C")
                {
                    throw new InvalidOperationException(
                        "Apenas créditos podem ser feitos em contas de outros usuários");
                }
            }

            // 4. Validar conta
            var conta = await _contaRepository.ObterPorIdAsync(contaIdDestino, cancellationToken)
                ?? throw new ContaNaoEncontradaException();

            if (!conta.Ativo)
                throw new ContaInativaException();

            // 5. Validar valor
            if (request.Valor <= 0)
                throw new ValorInvalidoException();

            // 6. Validar tipo de movimento
            if (request.TipoMovimento != "C" && request.TipoMovimento != "D")
                throw new TipoMovimentoInvalidoException();

            // 7. Para débitos, verificar saldo suficiente
            if (request.TipoMovimento == "D")
            {
                var saldoAtual = await _movimentoRepository.CalcularSaldoAsync(contaIdDestino, cancellationToken);
                if (saldoAtual < request.Valor)
                    throw new SaldoInsuficienteException();
            }

            // 8. Criar movimento
            var movimento = new Movimento(
                contaIdDestino,
                request.TipoMovimento[0],
                request.Valor,
                request.Descricao);

            var movimentoSalvo = await _movimentoRepository.InserirAsync(movimento, cancellationToken);
            var saldoNovo = await _movimentoRepository.CalcularSaldoAsync(contaIdDestino, cancellationToken);

            var encryptedMovimentoId = _encryptedIdService.Encrypt(movimentoSalvo.Id);

            // 9. Preparar resposta
            var response = new MovimentacaoResponse
            {
                MovimentoId = encryptedMovimentoId,
                DataMovimento = movimentoSalvo.DataMovimento,
                SaldoAtual = saldoNovo
            };

            // 10. Registrar idempotência com SUCESSO
            var resultadoSucesso = new ResultadoIdempotencia
            {
                MovimentoId = encryptedMovimentoId.Value,
                SaldoAtual = saldoNovo,
                DataProcessamento = DateTime.UtcNow
            };

            await _idempotenciaService.RegistrarAsync(
                request.IdentificacaoRequisicao,
                null,
                System.Text.Json.JsonSerializer.Serialize(resultadoSucesso),
                cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            // 11. Em caso de erro, registrar idempotência com ERRO
            var resultadoErro = new ResultadoIdempotencia
            {
                Erro = ex.Message,
                TipoErro = ex is DomainException domainEx ? domainEx.ErrorType : "INTERNAL_ERROR",
                DataProcessamento = DateTime.UtcNow
            };

            await _idempotenciaService.RegistrarAsync(
                request.IdentificacaoRequisicao,
                null,
                System.Text.Json.JsonSerializer.Serialize(resultadoErro),
                cancellationToken);

            throw;
        }
    }

    // Classe interna para serialização do resultado
    private class ResultadoIdempotencia
    {
        public string? MovimentoId { get; set; }
        public decimal? SaldoAtual { get; set; }
        public string? Erro { get; set; }
        public string? TipoErro { get; set; }
        public DateTime DataProcessamento { get; set; }
    }
}
