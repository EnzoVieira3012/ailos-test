using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao;

public sealed record MovimentacaoResponse
{
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType MovimentoId { get; init; }
    public required DateTime DataMovimento { get; init; }
    public required decimal SaldoAtual { get; init; }
}
