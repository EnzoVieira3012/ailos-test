using System.Security.Claims;
using Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao.Request;
using Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao.Response;
using Ailos.ContaCorrente.Api.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ailos.ContaCorrente.Api.Presentation.Controllers;

[ApiController]
[Route("api/movimentacao")]
[Authorize]
public class MovimentacaoController : ControllerBase
{
    private readonly IMovimentacaoService _service;

    public MovimentacaoController(IMovimentacaoService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MovimentacaoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarMovimentacao(
        [FromBody] MovimentacaoRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contaIdUsuarioLogado = GetContaIdFromToken();
            var response = await _service.CriarMovimentacaoAsync(
                contaIdUsuarioLogado, request, cancellationToken);
            
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Data["errorType"] != null)
        {
            var errorType = ex.Data["errorType"] as string;
            return BadRequest(new ProblemDetails
            {
                Title = "Falha na movimentação",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = errorType }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Falha na movimentação",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (ArgumentException ex) when (ex.Data["errorType"] != null)
        {
            var errorType = ex.Data["errorType"] as string;
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = errorType }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    private long GetContaIdFromToken()
    {
        var contaIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("contaId")?.Value;
        
        if (string.IsNullOrEmpty(contaIdClaim) || !long.TryParse(contaIdClaim, out var contaId))
            throw new UnauthorizedAccessException("Token inválido - contaId não encontrado");

        return contaId;
    }
}
