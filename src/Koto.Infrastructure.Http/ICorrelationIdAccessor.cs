namespace Koto.Infrastructure.Http;

/// <summary>
/// Provides the current correlation ID for outbound HTTP requests.
/// Implement this in your application (e.g. reading from <c>IHttpContextAccessor</c>).
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>Returns the current correlation ID, or <c>null</c> if not available.</summary>
    string? GetCorrelationId();
}
