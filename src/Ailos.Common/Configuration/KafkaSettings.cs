namespace Ailos.Common.Configuration;

public class KafkaSettings
{
    public const string SectionName = "Kafka";
    
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string TransferenciasTopic { get; set; } = "transferencias-realizadas";
    public string TarifasTopic { get; set; } = "tarifas-processadas";
    public string ConsumerGroup { get; set; } = "default-group";
}
