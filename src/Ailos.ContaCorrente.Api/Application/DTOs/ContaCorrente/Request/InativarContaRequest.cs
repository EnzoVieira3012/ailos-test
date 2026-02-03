namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente.Request;
public sealed record InativarContaRequest
{
    public required string Senha { get; init; }
}
