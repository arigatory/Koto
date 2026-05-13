using Koto.Domain;

namespace Koto.EventSourcing.Marten;

/// <summary>
/// Base class for event-sourced aggregate roots. State changes are modelled exclusively
/// as domain events; the aggregate is reconstituted by replaying them.
/// </summary>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public abstract class EventSourcedAggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    /// <summary>Initializes a new aggregate with the given <paramref name="id"/>.</summary>
    protected EventSourcedAggregateRoot(TId id) : base(id) { }

    /// <summary>Parameterless constructor for reconstitution.</summary>
    protected EventSourcedAggregateRoot() { }

    /// <summary>Events raised since the last <see cref="ClearUncommittedEvents"/> call.</summary>
    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    /// <summary>
    /// Applies <paramref name="event"/> to the aggregate state and records it as uncommitted.
    /// </summary>
    protected void RaiseEvent(IDomainEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    /// <summary>Mutates aggregate state in response to an event.</summary>
    protected abstract void Apply(IDomainEvent @event);

    /// <summary>Clears uncommitted events after they have been persisted.</summary>
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    /// <summary>
    /// Creates an instance of <typeparamref name="TAgg"/> by replaying
    /// <paramref name="events"/> through <see cref="Apply"/>.
    /// </summary>
    public static TAgg Reconstitute<TAgg>(IEnumerable<IDomainEvent> events)
        where TAgg : EventSourcedAggregateRoot<TId>
    {
        var aggregate = (TAgg)Activator.CreateInstance(typeof(TAgg), nonPublic: true)!;
        foreach (var @event in events)
            aggregate.Apply(@event);
        return aggregate;
    }
}
