using Wolverine;

namespace Koto.Messaging.Wolverine.Middleware;

/// <summary>
/// Wolverine middleware that propagates <see cref="Envelope.CorrelationId"/> into
/// <see cref="CorrelationContext.CorrelationId"/> for the duration of the handler call.
/// Register via <c>opts.Policies.AddMiddleware&lt;CorrelationIdMiddleware&gt;()</c>.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>Extracts the correlation ID from the envelope and sets it on the ambient context.</summary>
    public Task Before(Envelope envelope)
    {
        CorrelationContext.CorrelationId.Value = envelope.CorrelationId;
        return Task.CompletedTask;
    }
}
