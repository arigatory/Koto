using System.Text.Json;
using AwesomeAssertions;
using Koto.Application;

namespace Koto.Application.Tests;

public class IntegrationEventSerializationTests
{
    private sealed record OrderPlacedIntegrationEvent(Guid OrderId, decimal Total) : IntegrationEvent;

    [Fact]
    public void EventId_OccurredAt_and_CorrelationId_survive_json_round_trip()
    {
        var original = new OrderPlacedIntegrationEvent(Guid.NewGuid(), 99.5m)
        {
            CorrelationId = "corr-42",
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<OrderPlacedIntegrationEvent>(json)!;

        // Consumers deduplicate by EventId — it must be the producer's id, not a fresh one.
        restored.EventId.Should().Be(original.EventId);
        restored.OccurredAt.Should().Be(original.OccurredAt);
        restored.CorrelationId.Should().Be("corr-42");
        restored.Total.Should().Be(99.5m);
    }
}
