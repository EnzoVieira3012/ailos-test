namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

public sealed record InativarContaRequest
{
    public required string Senha { get; init; }
}
