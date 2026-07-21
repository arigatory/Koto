using Koto.Domain;
using Marten;

namespace Koto.EventSourcing.Marten;

/// <summary>
/// Marten-backed implementation of <see cref="IEventSourcedRepository{TAgg,TId}"/>.
/// Requires <typeparamref name="TId"/> to be a <see cref="StronglyTypedId{Guid}"/>
/// so the underlying <see cref="Guid"/> can be used as the Marten stream identity.
/// </summary>
public class MartenEventSourcedRepository<TAgg, TId> : IEventSourcedRepository<TAgg, TId>
    where TAgg : EventSourcedAggregateRoot<TId>
    where TId : StronglyTypedId<Guid>
{
    private readonly IDocumentSession _session;
    private readonly MartenAggregateTracker _tracker;

    /// <summary>Initializes a new <see cref="MartenEventSourcedRepository{TAgg,TId}"/>.</summary>
    public MartenEventSourcedRepository(IDocumentSession session, MartenAggregateTracker tracker)
    {
        _session = session;
        _tracker = tracker;
    }

    /// <inheritdoc/>
    public async Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        var events = await _session.Events.FetchStreamAsync(id.Value, token: ct).ConfigureAwait(false);
        if (events.Count == 0) return null;

        var domainEvents = events.Select(e => (IDomainEvent)e.Data);
        return EventSourcedAggregateRoot<TId>.Reconstitute<TAgg>(domainEvents);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(TAgg aggregate, CancellationToken ct = default)
    {
        Append(aggregate);
        await _session.SaveChangesAsync(ct).ConfigureAwait(false);
        _tracker.ClearAll();
    }

    /// <inheritdoc/>
    public void Append(TAgg aggregate)
    {
        var events = aggregate.UncommittedEvents;
        if (events.Count == 0) return;

        _session.Events.Append(aggregate.Id.Value, events.Cast<object>().ToArray());
        _tracker.Track(aggregate);
    }
}
