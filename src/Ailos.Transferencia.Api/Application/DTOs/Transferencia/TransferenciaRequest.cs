using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.Transferencia.Api.Application.DTOs.Transferencia;

public sealed record TransferenciaRequest
{
    public required string IdentificacaoRequisicao { get; init; }
    
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType ContaDestinoId { get; init; }
    
    public required decimal Valor { get; init; }
    public string? Descricao { get; init; }
}
