namespace Koto.Domain;

/// <summary>
/// Marker interface for domain events. Domain events are internal to a service
/// and must never be published directly to external systems.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique identifier for this event instance.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }
}
