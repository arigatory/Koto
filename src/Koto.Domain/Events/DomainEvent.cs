namespace Koto.Domain;

/// <summary>
/// Base record for domain events. Automatically assigns <see cref="EventId"/> and
/// <see cref="OccurredAt"/> on construction.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
