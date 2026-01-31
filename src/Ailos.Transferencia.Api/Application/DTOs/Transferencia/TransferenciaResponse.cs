using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.Transferencia.Api.Application.DTOs.Transferencia;

public sealed record TransferenciaResponse
{
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType TransferenciaId { get; init; }
    
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType ContaOrigemId { get; init; }
    
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType ContaDestinoId { get; init; }
    
    public required decimal Valor { get; init; }
    public decimal? TarifaAplicada { get; init; }
    public required DateTime DataMovimento { get; init; }
    public required string Status { get; init; }
}
