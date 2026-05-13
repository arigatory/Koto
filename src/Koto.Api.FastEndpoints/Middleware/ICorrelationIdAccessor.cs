namespace Koto.Api.FastEndpoints.Middleware;

/// <summary>Read-only access to the correlation ID for the current request.</summary>
public interface ICorrelationIdAccessor
{
    /// <summary>The correlation ID for the current request, or <c>null</c> if not set.</summary>
    string? Current { get; }
}

/// <summary>Default implementation that reads from <see cref="CorrelationContext"/>.</summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    /// <inheritdoc/>
    public string? Current => CorrelationContext.Current.Value;
}
