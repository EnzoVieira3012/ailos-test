using Ailos.EncryptedId;
using Ailos.Transferencia.Api.Application.DTOs.Transferencia;
using Ailos.Transferencia.Api.Application.Services;
using Ailos.Transferencia.Api.Domain.Exceptions;
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

    public TransferenciaController(
        ITransferenciaService transferenciaService,
        IEncryptedIdService encryptedIdService)
    {
        _transferenciaService = transferenciaService;
        _encryptedIdService = encryptedIdService;
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
            var contaIdUsuarioLogado = GetContaIdFromToken();
            var response = await _transferenciaService.CriarTransferenciaAsync(
                contaIdUsuarioLogado, request, cancellationToken);
            
            return Ok(response);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Falha na transferência",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = ex.ErrorType }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("idempotente"))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Requisição duplicada",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = "DUPLICATE_REQUEST" }
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
        var contaIdUsuarioLogado = GetContaIdFromToken();
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
        
        return Ok(response);
    }

    private long GetContaIdFromToken()
    {
        var contaIdClaim = User.FindFirst("contaId")?.Value;
        if (string.IsNullOrEmpty(contaIdClaim) || !long.TryParse(contaIdClaim, out var contaId))
            throw new UnauthorizedAccessException("Token inválido");

        return contaId;
    }
}
