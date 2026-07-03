using Koto.Application;
using Koto.Messaging.Wolverine.Middleware;
using Microsoft.Extensions.Logging;

namespace Koto.Messaging.Wolverine.Consuming;

/// <summary>
/// Base class for Kafka integration event consumers.
/// Provides idempotency checking, structured logging, and DLQ routing via exception re-throw.
/// </summary>
/// <typeparam name="TEvent">The integration event type to handle.</typeparam>
public abstract class IntegrationEventConsumerBase<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly IProcessedMessageStore _store;
    private readonly ILogger _logger;

    /// <summary>Initializes a new consumer with the required dependencies.</summary>
    protected IntegrationEventConsumerBase(
        IProcessedMessageStore store,
        ILogger<IntegrationEventConsumerBase<TEvent>> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Wolverine-discovered handler method.
    /// Checks idempotency, delegates to <see cref="ConsumeAsync"/>, then marks the event as processed.
    /// Unhandled exceptions are re-thrown so Wolverine routes them to the configured DLQ.
    /// </summary>
    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        var correlationId = CorrelationContext.CorrelationId.Value;

        if (await _store.IsProcessedAsync(@event.EventId, ct).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "Duplicate event {EventId} of type {EventType} (CorrelationId: {CorrelationId}), skipping",
                @event.EventId, typeof(TEvent).Name, correlationId);
            return;
        }

        try
        {
            _logger.LogInformation(
                "Handling event {EventId} of type {EventType} (CorrelationId: {CorrelationId})",
                @event.EventId, typeof(TEvent).Name, correlationId);

            await ConsumeAsync(@event, ct).ConfigureAwait(false);
            await _store.MarkAsProcessedAsync(@event.EventId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle event {EventId} of type {EventType} (CorrelationId: {CorrelationId})",
                @event.EventId, typeof(TEvent).Name, correlationId);
            throw; // Wolverine routes to DLQ based on configured error policy
        }
    }

    /// <summary>Implement domain-specific handling for <paramref name="event"/>.</summary>
    protected abstract Task ConsumeAsync(TEvent @event, CancellationToken ct);
}
