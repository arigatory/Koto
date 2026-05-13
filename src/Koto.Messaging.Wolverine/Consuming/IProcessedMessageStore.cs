namespace Koto.Messaging.Wolverine.Consuming;

/// <summary>
/// Tracks processed message IDs to support idempotent consumers.
/// Implement with a durable store (PostgreSQL, Redis) in production.
/// </summary>
public interface IProcessedMessageStore
{
    /// <summary>Returns <c>true</c> if <paramref name="messageId"/> has already been processed.</summary>
    Task<bool> IsProcessedAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Records <paramref name="messageId"/> as successfully processed.</summary>
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
}
