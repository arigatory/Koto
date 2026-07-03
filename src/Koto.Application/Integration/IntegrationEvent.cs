namespace Koto.Application;

/// <summary>
/// Base record for integration events. Automatically assigns <see cref="EventId"/> and
/// <see cref="OccurredAt"/> on construction; both are <c>init</c>-settable so that
/// deserialization on the consumer side preserves the producer's identity instead of
/// generating a fresh one (idempotency/deduplication depends on this).
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public string? CorrelationId { get; init; }
}
