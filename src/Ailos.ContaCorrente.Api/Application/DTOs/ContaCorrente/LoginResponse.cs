using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

public sealed record LoginResponse
{
    public required string Token { get; init; }
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType ContaId { get; init; }
    public required int NumeroConta { get; init; }
}
