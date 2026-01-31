using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

public sealed record CadastrarContaResponse
{
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType Id { get; init; }
    public required int Numero { get; init; }
}
