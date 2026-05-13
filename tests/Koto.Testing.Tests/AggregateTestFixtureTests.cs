using AwesomeAssertions;
using Koto.Domain;
using Koto.Testing.Aggregates;

namespace Koto.Testing.Tests;

public class AggregateTestFixtureTests
{
    // ── Fake domain ────────────────────────────────────────────────────────────

    private sealed record ItemAdded(string Item) : DomainEvent;
    private sealed record ItemRemoved(string Item) : DomainEvent;

    private sealed class Basket : AggregateRoot<Guid>, IAggregateApply<ItemAdded>
    {
        public List<string> Items { get; } = [];

        public void AddItem(string item)
        {
            Items.Add(item);
            AddDomainEvent(new ItemAdded(item));
        }

        public void RemoveItem(string item)
        {
            Items.Remove(item);
            AddDomainEvent(new ItemRemoved(item));
        }

        public void Apply(ItemAdded e) => Items.Add(e.Item);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public void When_raises_expected_event()
    {
        new AggregateTestFixture<Basket>()
            .When(b => b.AddItem("apple"))
            .Then()
            .ShouldHaveRaisedEvent<ItemAdded>(e => e.Item == "apple");
    }

    [Fact]
    public void ShouldHaveRaisedExactly_passes_when_count_matches()
    {
        new AggregateTestFixture<Basket>()
            .When(b =>
            {
                b.AddItem("apple");
                b.AddItem("banana");
            })
            .Then()
            .ShouldHaveRaisedExactly(2);
    }

    [Fact]
    public void ShouldNotHaveRaisedEvent_passes_when_event_absent()
    {
        new AggregateTestFixture<Basket>()
            .When(b => b.AddItem("apple"))
            .Then()
            .ShouldNotHaveRaisedEvent<ItemRemoved>();
    }

    [Fact]
    public void ShouldHaveRaisedNoEvents_passes_when_no_action_taken()
    {
        new AggregateTestFixture<Basket>()
            .When(_ => { })
            .Then()
            .ShouldHaveRaisedNoEvents();
    }

    [Fact]
    public void Given_restores_state_via_IAggregateApply()
    {
        Basket? capturedBasket = null;

        new AggregateTestFixture<Basket>()
            .Given(new ItemAdded("prior-item"))
            .When(b => { capturedBasket = b; b.AddItem("new-item"); })
            .Then()
            .ShouldHaveRaisedEvent<ItemAdded>(e => e.Item == "new-item");

        capturedBasket!.Items.Should().Contain("prior-item");
    }

    [Fact]
    public void Given_clears_uncommitted_events_before_When()
    {
        new AggregateTestFixture<Basket>()
            .Given(new ItemAdded("prior"))
            .When(b => b.AddItem("new"))
            .Then()
            .ShouldHaveRaisedExactly(1); // only "new", not "prior"
    }

    [Fact]
    public void And_returns_same_assertions_for_chaining()
    {
        new AggregateTestFixture<Basket>()
            .When(b => b.AddItem("x"))
            .Then()
            .ShouldHaveRaisedEvent<ItemAdded>()
            .And.ShouldHaveRaisedExactly(1)
            .And.ShouldNotHaveRaisedEvent<ItemRemoved>();
    }
}
