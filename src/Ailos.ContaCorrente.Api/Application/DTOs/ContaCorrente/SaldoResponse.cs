using System.Text.Json.Serialization;
using Ailos.EncryptedId.JsonConverters;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

public sealed record SaldoResponse
{
    [JsonConverter(typeof(EncryptedIdJsonConverter))]
    public required EncryptedIdType ContaId { get; init; }
    public required int NumeroConta { get; init; }
    public required string NomeTitular { get; init; }
    public required DateTime DataConsulta { get; init; }
    public required decimal Saldo { get; init; }
}
