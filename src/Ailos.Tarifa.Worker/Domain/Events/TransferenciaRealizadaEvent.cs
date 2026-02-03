using System.Text.Json.Serialization;

namespace Ailos.Tarifa.Worker.Domain.Events;

public class TransferenciaRealizadaEvent
{
    [JsonPropertyName("TransferenciaId")]
    public long TransferenciaId { get; set; }
    
    [JsonPropertyName("ContaOrigemId")]
    public long ContaOrigemId { get; set; }
    
    [JsonPropertyName("ContaDestinoId")]
    public long ContaDestinoId { get; set; }
    
    [JsonPropertyName("Valor")]
    public decimal Valor { get; set; }
    
    [JsonPropertyName("TarifaAplicada")]
    public decimal TarifaAplicada { get; set; }
    
    [JsonPropertyName("DataMovimento")]
    public DateTime DataMovimento { get; set; }
    
    [JsonPropertyName("IdentificacaoRequisicao")]
    public string? IdentificacaoRequisicao { get; set; }
}
