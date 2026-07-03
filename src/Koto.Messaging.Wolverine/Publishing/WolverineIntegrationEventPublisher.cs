using Koto.Application;
using Wolverine;

namespace Koto.Messaging.Wolverine.Publishing;

/// <summary>
/// Publishes integration events to the Wolverine message bus.
/// Wolverine routes the event to the appropriate Kafka topic based on its registered endpoints.
/// </summary>
public sealed class WolverineIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new <see cref="WolverineIntegrationEventPublisher"/>.</summary>
    public WolverineIntegrationEventPublisher(IMessageBus bus) => _bus = bus;

    /// <inheritdoc/>
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
        => await _bus.PublishAsync(integrationEvent).ConfigureAwait(false);
}
