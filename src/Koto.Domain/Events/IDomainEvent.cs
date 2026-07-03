namespace Koto.Domain;

/// <summary>
/// Marker interface for domain events. Domain events are internal to a service
/// and must never be published directly to external systems.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique identifier for this event instance. Survives serialization round-trips.</summary>
    Guid EventId { get; }

    /// <summary>Timestamp when the event occurred (UTC by default). Survives serialization round-trips.</summary>
    DateTimeOffset OccurredAt { get; }
}
