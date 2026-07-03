using System.Text.Json;
using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class DomainEventSerializationTests
{
    private sealed record OrderCreated(Guid OrderId) : DomainEvent;

    [Fact]
    public void EventId_and_OccurredAt_survive_json_round_trip()
    {
        var original = new OrderCreated(Guid.NewGuid());

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<OrderCreated>(json)!;

        restored.EventId.Should().Be(original.EventId);
        restored.OccurredAt.Should().Be(original.OccurredAt);
        restored.OrderId.Should().Be(original.OrderId);
    }

    [Fact]
    public void Two_events_get_distinct_ids()
    {
        var a = new OrderCreated(Guid.NewGuid());
        var b = new OrderCreated(Guid.NewGuid());

        a.EventId.Should().NotBe(b.EventId);
    }
}
