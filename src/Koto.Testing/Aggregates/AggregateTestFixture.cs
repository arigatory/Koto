using Koto.Domain;

namespace Koto.Testing.Aggregates;

/// <summary>
/// Fluent test fixture for DDD aggregates.
/// Reconstruct state via <see cref="Given"/>, trigger behaviour via <see cref="When"/>,
/// then assert via <see cref="Then"/>.
/// </summary>
/// <typeparam name="TAgg">The aggregate type under test. Must implement <see cref="IHasDomainEvents"/>.</typeparam>
public sealed class AggregateTestFixture<TAgg>
    where TAgg : class, IHasDomainEvents
{
    private TAgg _aggregate = CreateInstance();
    private readonly List<IDomainEvent> _raisedEvents = [];

    /// <summary>
    /// Pre-loads the aggregate with events that represent prior state.
    /// Each event is applied via <see cref="IAggregateApply{TEvent}"/> if implemented,
    /// then the uncommitted event queue is cleared so <see cref="When"/> starts clean.
    /// </summary>
    public AggregateTestFixture<TAgg> Given(params IDomainEvent[] priorEvents)
    {
        _aggregate = CreateInstance();
        foreach (var e in priorEvents)
            ApplyEvent(_aggregate, e);

        _aggregate.ClearDomainEvents();
        _raisedEvents.Clear();
        return this;
    }

    /// <summary>Executes the action under test on the aggregate.</summary>
    public AggregateTestFixture<TAgg> When(Action<TAgg> act)
    {
        act(_aggregate);
        _raisedEvents.AddRange(_aggregate.DomainEvents);
        return this;
    }

    /// <summary>Returns an assertions object for verifying raised domain events.</summary>
    public AggregateAssertions<TAgg> Then() => new(_aggregate, _raisedEvents);

    // Uses nonPublic: true so aggregates with protected constructors (ORM pattern) work.
    private static TAgg CreateInstance() =>
        (TAgg)Activator.CreateInstance(typeof(TAgg), nonPublic: true)!;

    private static void ApplyEvent(TAgg aggregate, IDomainEvent @event)
    {
        var applyInterface = typeof(IAggregateApply<>).MakeGenericType(@event.GetType());
        if (applyInterface.IsAssignableFrom(typeof(TAgg)))
        {
            var method = applyInterface.GetMethod(nameof(IAggregateApply<IDomainEvent>.Apply));
            method?.Invoke(aggregate, [@event]);
        }
    }
}

/// <summary>
/// Optional interface for aggregates that want to apply prior events during
/// <see cref="AggregateTestFixture{TAgg}.Given"/> reconstruction.
/// </summary>
public interface IAggregateApply<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>Applies <paramref name="event"/> to rebuild aggregate state without raising new events.</summary>
    void Apply(TEvent @event);
}
