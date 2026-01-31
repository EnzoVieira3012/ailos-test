namespace Ailos.Tarifa.Worker.Domain.Entities;

public class TarifaEntity
{
    public long Id { get; set; }
    public long ContaCorrenteId { get; set; }
    public long? TransferenciaId { get; set; }
    public DateTime DataMovimento { get; set; }
    public decimal Valor { get; set; }
    public bool Processada { get; set; }
    public string? MensagemErro { get; set; }
    public DateTime? DataProcessamento { get; set; }
}

public class TarifaProcessadaEntity
{
    public long Id { get; set; }
    public long TransferenciaId { get; set; }
    public long ContaOrigemId { get; set; }
    public decimal ValorTarifa { get; set; }
    public DateTime DataProcessamento { get; set; }
    public string Status { get; set; } = "SUCESSO";
    public string? Mensagem { get; set; }
    public string TopicoKafka { get; set; } = string.Empty;
    public long OffsetKafka { get; set; }
}

public enum StatusProcessamento
{
    Pendente,
    Processando,
    Sucesso,
    Falha,
    Duplicado
}
