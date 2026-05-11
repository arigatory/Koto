namespace Koto.Application;

/// <summary>
/// Publishes integration events to the message broker (e.g. Kafka via Wolverine).
/// Implemented by <c>Koto.Messaging.Wolverine</c>.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes <paramref name="integrationEvent"/> to the message broker.</summary>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default);
}
