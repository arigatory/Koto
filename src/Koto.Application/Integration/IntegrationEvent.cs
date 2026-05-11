namespace Koto.Application;

/// <summary>
/// Base record for integration events. Automatically assigns <see cref="EventId"/> and
/// <see cref="OccurredAt"/> on construction.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <inheritdoc/>
    public string? CorrelationId { get; init; }
}
