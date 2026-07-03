using Koto.Domain;
using AwesomeAssertions;

namespace Koto.Domain.Tests;

public class AggregateRootTests
{
    private sealed record OrderCreated(Guid OrderId) : DomainEvent;
    private sealed record OrderShipped(Guid OrderId) : DomainEvent;

    private sealed class Order : AggregateRoot<Guid>
    {
        public Order(Guid id) : base(id) { }

        public void Create() => AddDomainEvent(new OrderCreated(Id));
        public void Ship() => AddDomainEvent(new OrderShipped(Id));
    }

    [Fact]
    public void AddDomainEvent_appends_to_list()
    {
        var order = new Order(Guid.NewGuid());
        order.Create();

        order.DomainEvents.Should().HaveCount(1);
        order.DomainEvents[0].Should().BeOfType<OrderCreated>();
    }

    [Fact]
    public void Multiple_events_are_ordered()
    {
        var order = new Order(Guid.NewGuid());
        order.Create();
        order.Ship();

        order.DomainEvents.Should().HaveCount(2);
        order.DomainEvents[0].Should().BeOfType<OrderCreated>();
        order.DomainEvents[1].Should().BeOfType<OrderShipped>();
    }

    [Fact]
    public void ClearDomainEvents_empties_the_list()
    {
        var order = new Order(Guid.NewGuid());
        order.Create();
        order.ClearDomainEvents();

        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvent_has_unique_id_and_utc_timestamp()
    {
        var order = new Order(Guid.NewGuid());
        order.Create();

        var evt = order.DomainEvents[0];
        evt.EventId.Should().NotBe(Guid.Empty);
        evt.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
