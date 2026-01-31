using System.Text.Json.Serialization;
using Ailos.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

public sealed record CadastrarContaRequest
{
    public required string Cpf { get; init; }
    public required string Senha { get; init; }
    public required string Nome { get; init; }
}
