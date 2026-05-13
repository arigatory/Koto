using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Testing.Aggregates;

/// <summary>
/// Fluent assertions for domain events raised by an aggregate under test.
/// Obtained via <see cref="AggregateTestFixture{TAgg}.Then"/>.
/// </summary>
/// <typeparam name="TAgg">The aggregate type under test.</typeparam>
public sealed class AggregateAssertions<TAgg>
    where TAgg : class, IHasDomainEvents
{
    private readonly TAgg _aggregate;
    private readonly IReadOnlyList<IDomainEvent> _raisedEvents;

    internal AggregateAssertions(TAgg aggregate, IReadOnlyList<IDomainEvent> raisedEvents)
    {
        _aggregate = aggregate;
        _raisedEvents = raisedEvents;
    }

    /// <summary>Chains additional assertions.</summary>
    public AggregateAssertions<TAgg> And => this;

    /// <summary>Asserts that exactly <paramref name="count"/> domain events were raised.</summary>
    public AggregateAssertions<TAgg> ShouldHaveRaisedExactly(int count)
    {
        _raisedEvents.Should().HaveCount(count,
            $"expected exactly {count} domain event(s) to be raised, but found {_raisedEvents.Count}");
        return this;
    }

    /// <summary>
    /// Asserts that at least one event of type <typeparamref name="TEvent"/> was raised,
    /// optionally matching <paramref name="predicate"/>.
    /// </summary>
    public AggregateAssertions<TAgg> ShouldHaveRaisedEvent<TEvent>(
        Func<TEvent, bool>? predicate = null)
        where TEvent : IDomainEvent
    {
        var matching = _raisedEvents.OfType<TEvent>().ToList();

        matching.Should().NotBeEmpty(
            $"expected at least one {typeof(TEvent).Name} to be raised, " +
            $"but found events: [{string.Join(", ", _raisedEvents.Select(e => e.GetType().Name))}]");

        if (predicate is not null)
            matching.Should().Contain(e => predicate(e),
                $"no {typeof(TEvent).Name} matched the given predicate");

        return this;
    }

    /// <summary>Asserts that no event of type <typeparamref name="TEvent"/> was raised.</summary>
    public AggregateAssertions<TAgg> ShouldNotHaveRaisedEvent<TEvent>()
        where TEvent : IDomainEvent
    {
        _raisedEvents.OfType<TEvent>().Should().BeEmpty(
            $"expected no {typeof(TEvent).Name} to be raised");
        return this;
    }

    /// <summary>Asserts that no domain events were raised at all.</summary>
    public AggregateAssertions<TAgg> ShouldHaveRaisedNoEvents()
    {
        _raisedEvents.Should().BeEmpty("expected no domain events to be raised");
        return this;
    }
}
