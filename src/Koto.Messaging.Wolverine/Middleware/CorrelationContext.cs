namespace Koto.Messaging.Wolverine.Middleware;

/// <summary>
/// Ambient correlation ID for the current async execution context.
/// Set by <see cref="CorrelationIdMiddleware"/> from the incoming Wolverine envelope.
/// </summary>
public static class CorrelationContext
{
    /// <summary>The correlation ID for the currently executing message handler, or <c>null</c> if unset.</summary>
    public static readonly AsyncLocal<string?> CorrelationId = new();
}
