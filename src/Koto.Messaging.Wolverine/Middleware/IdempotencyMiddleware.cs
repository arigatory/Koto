using Koto.Messaging.Wolverine.Consuming;
using Wolverine;

namespace Koto.Messaging.Wolverine.Middleware;

/// <summary>
/// Wolverine middleware that deduplicates messages using <see cref="IProcessedMessageStore"/>.
/// Register via <c>opts.Policies.AddMiddleware&lt;IdempotencyMiddleware&gt;()</c>.
/// Complements <see cref="IntegrationEventConsumerBase{TEvent}"/> — use one or the other,
/// not both, to avoid double-checking.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly IProcessedMessageStore _store;

    /// <summary>Initializes the middleware with the message store injected by Wolverine.</summary>
    public IdempotencyMiddleware(IProcessedMessageStore store) => _store = store;

    /// <summary>Returns <see cref="HandlerContinuation.Stop"/> if the message has already been processed.</summary>
    public async Task<HandlerContinuation> Before(Envelope envelope, CancellationToken ct)
    {
        if (envelope.Id == Guid.Empty)
            return HandlerContinuation.Continue;

        return await _store.IsProcessedAsync(envelope.Id, ct)
            ? HandlerContinuation.Stop
            : HandlerContinuation.Continue;
    }

    /// <summary>Marks the message as processed after the handler completes successfully.</summary>
    public Task After(Envelope envelope, CancellationToken ct)
        => _store.MarkAsProcessedAsync(envelope.Id, ct);
}
