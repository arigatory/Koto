using AwesomeAssertions;
using Koto.Application;
using Koto.Messaging.Wolverine.Publishing;
using NSubstitute;
using Wolverine;

namespace Koto.Messaging.Wolverine.Tests;

public class WolverineIntegrationEventPublisherTests
{
    private sealed record OrderPlaced(string OrderId) : IntegrationEvent;

    [Fact]
    public async Task PublishAsync_delegates_to_message_bus()
    {
        var bus = Substitute.For<IMessageBus>();
        var publisher = new WolverineIntegrationEventPublisher(bus);
        var @event = new OrderPlaced("ord-1");

        await publisher.PublishAsync(@event);

        await bus.Received(1).PublishAsync(@event);
    }

    [Fact]
    public async Task PublishAsync_propagates_exceptions_from_bus()
    {
        var bus = Substitute.For<IMessageBus>();
        bus.PublishAsync(Arg.Any<object>()).Returns(x => throw new InvalidOperationException("bus down"));
        var publisher = new WolverineIntegrationEventPublisher(bus);

        var act = () => publisher.PublishAsync(new OrderPlaced("ord-2"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
