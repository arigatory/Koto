namespace Koto.EventSourcing.Marten;

/// <summary>
/// Persistence contract for event-sourced aggregate roots.
/// Unlike EF Core repositories, <see cref="SaveAsync"/> is explicit because Marten
/// does not use a shared Unit of Work / DbContext transaction.
/// </summary>
public interface IEventSourcedRepository<TAgg, TId>
    where TAgg : EventSourcedAggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Reconstitutes <typeparamref name="TAgg"/> from its event stream,
    /// or returns <c>null</c> if the stream does not exist.
    /// </summary>
    Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Appends uncommitted events to the aggregate's stream and saves them atomically.
    /// </summary>
    Task SaveAsync(TAgg aggregate, CancellationToken ct = default);
}
