using System.Text.Json;
using Confluent.Kafka;
using Koto.Application;

namespace Koto.Testing.Integration;

/// <summary>
/// Тестовый продюсер schema-прозрачной шины: публикует чистый web-cased JSON
/// (как <c>PublishIntegrationEvents</c> в проде), топик — из константы <c>Topic</c>
/// контракта. Симулирует и «настоящие» события, и redelivery.
/// </summary>
public sealed class RawJsonKafkaProducer(string bootstrapServers) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IProducer<Null, string> _producer = new ProducerBuilder<Null, string>(
        new ProducerConfig { BootstrapServers = bootstrapServers }).Build();

    /// <summary>Публикует событие в топик из его константы <c>Topic</c>.</summary>
    public Task PublishAsync<T>(T @event, CancellationToken ct = default)
        where T : IIntegrationEvent =>
        PublishAsync(IntegrationEventTopics.For<T>(), @event, ct);

    /// <summary>Публикует payload в явно указанный топик (для негативных сценариев).</summary>
    public async Task PublishAsync<T>(string topic, T @event, CancellationToken ct = default)
    {
        await _producer.ProduceAsync(topic, new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(@event, Json),
        }, ct).ConfigureAwait(false);
        _producer.Flush(TimeSpan.FromSeconds(10));
    }

    /// <inheritdoc />
    public void Dispose() => _producer.Dispose();
}
