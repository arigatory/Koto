namespace Koto.Domain;

/// <summary>
/// Base class for aggregate roots. Extends <see cref="Entity{TId}"/> with the ability
/// to collect <see cref="IDomainEvent"/>s raised during a business operation.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised since the last <see cref="ClearDomainEvents"/> call.</summary>
    /// <remarks>
    /// EF Core already skips this get-only collection by convention; the explicit
    /// <see cref="System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute"/> documents the
    /// intent and guards against non-standard mappers attempting to persist it.
    /// </remarks>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Initializes a new aggregate root with the given <paramref name="id"/>.</summary>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Parameterless constructor for ORM use.</summary>
    protected AggregateRoot() { }

    /// <summary>Appends <paramref name="domainEvent"/> to the list of uncommitted events.</summary>
    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    /// <summary>
    /// Removes all collected domain events. Call this after the events have been
    /// dispatched (e.g. after <c>SaveChangesAsync</c>).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
