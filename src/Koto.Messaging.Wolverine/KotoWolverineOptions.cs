namespace Koto.Messaging.Wolverine;

/// <summary>Configuration options for <c>Koto.Messaging.Wolverine</c>.</summary>
public sealed class KotoWolverineOptions
{
    /// <summary>
    /// Timeout for request/reply commands sent via <c>IIntegrationCommandDispatcher</c>.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RequestReplyTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time window for idempotency deduplication.
    /// Events/messages with an ID seen within this window are skipped.
    /// Defaults to 24 hours.
    /// </summary>
    public TimeSpan IdempotencyWindow { get; set; } = TimeSpan.FromHours(24);
}
