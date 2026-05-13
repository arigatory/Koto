using AwesomeAssertions;
using Koto.Application;
using Koto.Messaging.Wolverine.Consuming;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Koto.Messaging.Wolverine.Tests;

public class IntegrationEventConsumerBaseTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed record PaymentReceived(string PaymentId) : IntegrationEvent;

    private sealed class TestConsumer : IntegrationEventConsumerBase<PaymentReceived>
    {
        public List<PaymentReceived> Handled { get; } = [];
        public bool ThrowOnConsume { get; set; }

        public TestConsumer(IProcessedMessageStore store)
            : base(store, NullLogger<IntegrationEventConsumerBase<PaymentReceived>>.Instance) { }

        protected override Task ConsumeAsync(PaymentReceived @event, CancellationToken ct)
        {
            if (ThrowOnConsume) throw new InvalidOperationException("consumer failure");
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_processes_new_event_and_marks_as_processed()
    {
        var store = Substitute.For<IProcessedMessageStore>();
        store.IsProcessedAsync(Arg.Any<Guid>()).Returns(false);
        var consumer = new TestConsumer(store);
        var @event = new PaymentReceived("pay-1");

        await consumer.HandleAsync(@event, CancellationToken.None);

        consumer.Handled.Should().ContainSingle().Which.Should().Be(@event);
        await store.Received(1).MarkAsProcessedAsync(@event.EventId);
    }

    [Fact]
    public async Task HandleAsync_skips_duplicate_event()
    {
        var store = Substitute.For<IProcessedMessageStore>();
        store.IsProcessedAsync(Arg.Any<Guid>()).Returns(true);
        var consumer = new TestConsumer(store);

        await consumer.HandleAsync(new PaymentReceived("pay-2"), CancellationToken.None);

        consumer.Handled.Should().BeEmpty();
        await store.DidNotReceive().MarkAsProcessedAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task HandleAsync_rethrows_on_consume_failure_for_dlq_routing()
    {
        var store = Substitute.For<IProcessedMessageStore>();
        store.IsProcessedAsync(Arg.Any<Guid>()).Returns(false);
        var consumer = new TestConsumer(store) { ThrowOnConsume = true };

        var act = () => consumer.HandleAsync(new PaymentReceived("pay-3"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await store.DidNotReceive().MarkAsProcessedAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task HandleAsync_processes_two_different_events_independently()
    {
        var store = new InMemoryProcessedMessageStore(
            Microsoft.Extensions.Options.Options.Create(new KotoWolverineOptions()));
        var consumer = new TestConsumer(store);
        var e1 = new PaymentReceived("pay-4");
        var e2 = new PaymentReceived("pay-5");

        await consumer.HandleAsync(e1, CancellationToken.None);
        await consumer.HandleAsync(e2, CancellationToken.None);

        consumer.Handled.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_skips_same_event_on_retry_after_success()
    {
        var store = new InMemoryProcessedMessageStore(
            Microsoft.Extensions.Options.Options.Create(new KotoWolverineOptions()));
        var consumer = new TestConsumer(store);
        var @event = new PaymentReceived("pay-6");

        await consumer.HandleAsync(@event, CancellationToken.None);
        await consumer.HandleAsync(@event, CancellationToken.None); // simulated redelivery

        consumer.Handled.Should().ContainSingle();
    }
}
