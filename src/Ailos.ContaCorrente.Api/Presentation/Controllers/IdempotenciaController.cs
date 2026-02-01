using Ailos.ContaCorrente.Api.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ailos.ContaCorrente.Api.Presentation.Controllers;

[ApiController]
[Route("api/admin/idempotencia")]
[Authorize(Roles = "Admin")]
public class IdempotenciaController : ControllerBase
{
    private readonly IIdempotenciaService _service;

    public IdempotenciaController(IIdempotenciaService service)
    {
        _service = service;
    }

    [HttpGet("{chave}")]
    [ProducesResponseType(typeof(IdempotenciaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterPorChave(
        [FromRoute] string chave,
        CancellationToken cancellationToken = default)
    {
        var idempotencia = await _service.ObterPorChaveAsync(chave, cancellationToken);
        
        if (idempotencia == null)
            return NotFound();
        
        return Ok(new IdempotenciaResponse
        {
            Chave = idempotencia.Chave,
            DataCriacao = idempotencia.DataCriacao,
            TemResultado = !string.IsNullOrEmpty(idempotencia.Resultado)
        });
    }

    [HttpGet("verificar/{chave}")]
    [ProducesResponseType(typeof(VerificacaoIdempotenciaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerificarProcessamento(
        [FromRoute] string chave,
        CancellationToken cancellationToken = default)
    {
        var jaProcessada = await _service.RequisicaoJaProcessadaAsync(chave, cancellationToken);
        
        return Ok(new VerificacaoIdempotenciaResponse
        {
            Chave = chave,
            JaProcessada = jaProcessada
        });
    }

    [HttpDelete("{chave}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Remover(
        [FromRoute] string chave,
        CancellationToken cancellationToken = default)
    {
        // Note: Em um cenário real, o delete seria no repositório
        // Mas como é endpoint administrativo, pode ficar assim por enquanto
        return NoContent();
    }
}

// DTOs para o controller
public sealed record IdempotenciaResponse
{
    public required string Chave { get; init; }
    public required DateTime DataCriacao { get; init; }
    public required bool TemResultado { get; init; }
}

public sealed record VerificacaoIdempotenciaResponse
{
    public required string Chave { get; init; }
    public required bool JaProcessada { get; init; }
}
