using Ailos.EncryptedId;
using Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente.Request;
using Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente.Response;
using Ailos.ContaCorrente.Api.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ailos.ContaCorrente.Api.Presentation.Controllers;

[ApiController]
[Route("api/contacorrente")]
public class ContaCorrenteController : ControllerBase
{
    private readonly IContaCorrenteService _service;
    private readonly IEncryptedIdService _encryptedIdService;

    public ContaCorrenteController(
        IContaCorrenteService service,
        IEncryptedIdService encryptedIdService)
    {
        _service = service;
        _encryptedIdService = encryptedIdService;
    }

    [HttpPost("cadastrar")]
    [ProducesResponseType(typeof(CadastrarContaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarContaRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _service.CadastrarAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = "INVALID_DOCUMENT" }
            });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _service.LoginAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciais inválidas",
                Detail = "CPF/Número da conta ou senha incorretos",
                Status = StatusCodes.Status401Unauthorized,
                Extensions = { ["errorType"] = "USER_UNAUTHORIZED" }
            });
        }
    }

    [Authorize]
    [HttpPut("inativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Inativar(
        [FromBody] InativarContaRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contaId = GetContaIdFromToken();
            await _service.InativarAsync(contaId, request.Senha, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Não foi possível inativar a conta",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = "INVALID_ACCOUNT" }
            });
        }
    }

    [Authorize]
    [HttpGet("saldo")]
    [ProducesResponseType(typeof(SaldoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConsultarSaldo(CancellationToken cancellationToken = default)
    {
        try
        {
            var contaId = GetContaIdFromToken();
            var response = await _service.ConsultarSaldoAsync(contaId, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Não foi possível consultar o saldo",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorType"] = "INVALID_ACCOUNT" }
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
