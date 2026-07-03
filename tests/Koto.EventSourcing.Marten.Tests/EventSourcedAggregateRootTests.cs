using AwesomeAssertions;
using Koto.Domain;
using Koto.EventSourcing.Marten;

namespace Koto.EventSourcing.Marten.Tests;

// ---------------------------------------------------------------------------
// Test doubles
// ---------------------------------------------------------------------------

public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}

public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, decimal Total) : IDomainEvent;
public sealed record OrderShipped(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed class Order : EventSourcedAggregateRoot<OrderId>
{
    public decimal Total { get; private set; }
    public bool IsShipped { get; private set; }

    public static Order Place(decimal total)
    {
        var order = new Order();
        order.RaiseEvent(new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, total));
        return order;
    }

    public void Ship() => RaiseEvent(new OrderShipped(Guid.NewGuid(), DateTime.UtcNow));

    protected override void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderPlaced e: Total = e.Total; break;
            case OrderShipped:  IsShipped = true; break;
        }
    }

    private Order() { }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public sealed class EventSourcedAggregateRootTests
{
    [Fact]
    public void RaiseEvent_applies_state_and_records_uncommitted()
    {
        var order = Order.Place(99.50m);

        order.Total.Should().Be(99.50m);
        order.UncommittedEvents.Should().HaveCount(1);
        order.UncommittedEvents[0].Should().BeOfType<OrderPlaced>();
    }

    [Fact]
    public void Multiple_events_accumulate_correctly()
    {
        var order = Order.Place(50m);
        order.Ship();

        order.IsShipped.Should().BeTrue();
        order.UncommittedEvents.Should().HaveCount(2);
    }

    [Fact]
    public void ClearUncommittedEvents_empties_the_list()
    {
        var order = Order.Place(10m);
        order.ClearUncommittedEvents();

        order.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reconstitute_replays_events_in_order()
    {
        var events = new IDomainEvent[]
        {
            new OrderPlaced(Guid.NewGuid(), DateTime.UtcNow, 55m),
            new OrderShipped(Guid.NewGuid(), DateTime.UtcNow),
        };

        var order = EventSourcedAggregateRoot<OrderId>.Reconstitute<Order>(events);

        order.Total.Should().Be(55m);
        order.IsShipped.Should().BeTrue();
        order.UncommittedEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reconstituted_aggregate_has_no_uncommitted_events()
    {
        var events = new IDomainEvent[]
        {
            new OrderPlaced(Guid.NewGuid(), DateTime.UtcNow, 100m),
        };

        var order = EventSourcedAggregateRoot<OrderId>.Reconstitute<Order>(events);
        order.UncommittedEvents.Should().BeEmpty();
    }
}
