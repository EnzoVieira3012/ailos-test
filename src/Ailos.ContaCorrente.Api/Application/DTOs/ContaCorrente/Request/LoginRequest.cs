namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente.Request;

public sealed record LoginRequest
{
    public string? Cpf { get; init; }
    public int? NumeroConta { get; init; }
    public required string Senha { get; init; }
}
