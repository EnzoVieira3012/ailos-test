using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao.Request;
public sealed record MovimentacaoRequest
{
    public required string IdentificacaoRequisicao { get; init; }
    
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public EncryptedIdType? ContaCorrenteId { get; init; }
    
    public required decimal Valor { get; init; }
    public required string TipoMovimento { get; init; } // "C" ou "D"
    public string? Descricao { get; init; }
}
