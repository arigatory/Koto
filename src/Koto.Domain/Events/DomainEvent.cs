namespace Koto.Domain;

/// <summary>
/// Base record for domain events. Automatically assigns <see cref="EventId"/> and
/// <see cref="OccurredAt"/> on construction; both are <c>init</c>-settable so that
/// deserialization (e.g. System.Text.Json) preserves the original identity instead
/// of generating a fresh one.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
