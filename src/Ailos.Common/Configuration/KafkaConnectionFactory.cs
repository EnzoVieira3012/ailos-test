using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Ailos.Common.Configuration;

public class KafkaConnectionFactory
{
    private readonly KafkaSettings _settings;

    public KafkaConnectionFactory(IOptions<KafkaSettings> options)
    {
        _settings = options.Value;
    }

    public IProducer<string, string> CreateProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            LingerMs = 5
        };

        return new ProducerBuilder<string, string>(config)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(Serializers.Utf8)
            .Build();
    }

    public IConsumer<string, string> CreateConsumer(string? groupId = null)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = groupId ?? _settings.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        return new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {})
            .Build();
    }
}
