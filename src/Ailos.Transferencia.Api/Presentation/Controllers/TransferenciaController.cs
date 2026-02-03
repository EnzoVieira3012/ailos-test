using Ailos.EncryptedId;
using Ailos.Transferencia.Api.Application.DTOs.Transferencia;
using Ailos.Transferencia.Api.Application.Services;
using Ailos.Common.Domain.Exceptions;
using Ailos.Common.Infrastructure.Security.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ailos.Transferencia.Api.Presentation.Controllers;

[ApiController]
[Route("api/transferencia")]
[Authorize]
public class TransferenciaController : ControllerBase
{
    private readonly ITransferenciaService _transferenciaService;
    private readonly IEncryptedIdService _encryptedIdService;
    private readonly ILogger<TransferenciaController> _logger;

    public TransferenciaController(
        ITransferenciaService transferenciaService,
        IEncryptedIdService encryptedIdService,
        ILogger<TransferenciaController> logger)
    {
        _transferenciaService = transferenciaService;
        _encryptedIdService = encryptedIdService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransferenciaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarTransferencia(
        [FromBody] TransferenciaRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Recebendo requisição de transferência: {Identificacao}", 
                request.IdentificacaoRequisicao);
            
            var contaIdUsuarioLogado = GetContaIdFromToken();
            var response = await _transferenciaService.CriarTransferenciaAsync(
                contaIdUsuarioLogado, request, cancellationToken);
            
            _logger.LogInformation("Transferência criada com sucesso: {TransferenciaId}", 
                response.TransferenciaId.Value);
            
            return Ok(response);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Erro de domínio na transferência");
            return BadRequest(new ProblemDetails
            {
                Title = "Falha na transferência",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = ex.ErrorCode }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("idempotente"))
        {
            _logger.LogWarning("Requisição duplicada detectada: {Identificacao}", 
                request.IdentificacaoRequisicao);
            return BadRequest(new ProblemDetails
            {
                Title = "Requisição duplicada",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = "DUPLICATE_REQUEST" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado na transferência");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Erro interno",
                Detail = "Ocorreu um erro interno no servidor",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    [HttpGet("historico")]
    [ProducesResponseType(typeof(IEnumerable<TransferenciaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObterHistorico(
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contaIdUsuarioLogado = GetContaIdFromToken();
            
            _logger.LogDebug("Consultando histórico para conta {ContaId}", contaIdUsuarioLogado);
            
            var transferencias = await _transferenciaService.ObterTransferenciasPorContaAsync(
                contaIdUsuarioLogado, cancellationToken);
            
            if (dataInicio.HasValue)
            {
                transferencias = transferencias.Where(t => t.DataMovimento >= dataInicio.Value);
            }
            
            if (dataFim.HasValue)
            {
                transferencias = transferencias.Where(t => t.DataMovimento <= dataFim.Value);
            }
            
            var response = transferencias.Select(t => new TransferenciaResponse
            {
                TransferenciaId = _encryptedIdService.Encrypt(t.Id),
                ContaOrigemId = _encryptedIdService.Encrypt(t.ContaCorrenteOrigemId),
                ContaDestinoId = _encryptedIdService.Encrypt(t.ContaCorrenteDestinoId),
                Valor = t.Valor,
                TarifaAplicada = t.TarifaAplicada,
                DataMovimento = t.DataMovimento,
                Status = t.Status.ToString()
            });
            
            _logger.LogInformation("Histórico retornado: {Quantidade} transferências", response.Count());
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar histórico");
            throw;
        }
    }

    private long GetContaIdFromToken()
    {
        try
        {
            var contaId = User.GetUserIdAsLong();
            _logger.LogDebug("ContaId extraído do token: {ContaId}", contaId);
            return contaId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao extrair contaId do token");
            throw new UnauthorizedAccessException("Token inválido ou expirado");
        }
    }
}