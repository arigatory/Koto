namespace Koto.Api.FastEndpoints.Middleware;

/// <summary>
/// Ambient correlation ID for the current HTTP request context.
/// Set by <see cref="CorrelationIdMiddleware"/> from the <c>X-Correlation-ID</c> request header.
/// </summary>
public static class CorrelationContext
{
    /// <summary>The correlation ID for the current request, or <c>null</c> if middleware is not registered.</summary>
    public static readonly AsyncLocal<string?> Current = new();
}
