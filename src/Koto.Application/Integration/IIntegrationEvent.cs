namespace Koto.Application;

/// <summary>
/// Marks an event that crosses a service boundary (published to Kafka / message broker).
/// Integration events are versioned public contracts — never change them in a
/// backward-incompatible way without creating a new version.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance. Must survive serialization round-trips —
    /// consumers rely on it for idempotency/deduplication.
    /// </summary>
    Guid EventId { get; }

    /// <summary>Timestamp when the event occurred (UTC by default). Survives serialization round-trips.</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>Optional correlation ID for distributed tracing.</summary>
    string? CorrelationId { get; }
}
