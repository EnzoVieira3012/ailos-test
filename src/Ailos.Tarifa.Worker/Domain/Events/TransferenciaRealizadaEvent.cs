using System.Text.Json.Serialization;

namespace Ailos.Tarifa.Worker.Domain.Events;

public class TransferenciaRealizadaEvent
{
    [JsonPropertyName("transferenciaId")]
    public long TransferenciaId { get; set; }
    
    [JsonPropertyName("contaOrigemId")]
    public long ContaOrigemId { get; set; }
    
    [JsonPropertyName("contaDestinoId")]
    public long ContaDestinoId { get; set; }
    
    [JsonPropertyName("valor")]
    public decimal Valor { get; set; }
    
    [JsonPropertyName("tarifaAplicada")]
    public decimal TarifaAplicada { get; set; }
    
    [JsonPropertyName("dataMovimento")]
    public DateTime DataMovimento { get; set; }
    
    [JsonPropertyName("identificacaoRequisicao")]
    public string? IdentificacaoRequisicao { get; set; }
}
