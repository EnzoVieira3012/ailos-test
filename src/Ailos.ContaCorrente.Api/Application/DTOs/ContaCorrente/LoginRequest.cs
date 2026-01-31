using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

public sealed record LoginRequest
{
    public string? Cpf { get; init; }
    public int? NumeroConta { get; init; }
    public required string Senha { get; init; }
}
