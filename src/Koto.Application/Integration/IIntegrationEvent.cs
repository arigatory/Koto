namespace Koto.Application;

/// <summary>
/// Marks an event that crosses a service boundary (published to Kafka / message broker).
/// Integration events are versioned public contracts — never change them in a
/// backward-incompatible way without creating a new version.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Unique identifier for this event instance.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }

    /// <summary>Optional correlation ID for distributed tracing.</summary>
    string? CorrelationId { get; }
}
