using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Koto.Messaging.Wolverine.Consuming;

/// <summary>
/// In-memory implementation of <see cref="IProcessedMessageStore"/>.
/// Suitable for development and testing only — state is lost on restart.
/// </summary>
public sealed class InMemoryProcessedMessageStore : IProcessedMessageStore
{
    private readonly ConcurrentDictionary<Guid, DateTime> _processed = new();
    private readonly TimeSpan _window;

    /// <summary>Initializes the store with the idempotency window from <see cref="KotoWolverineOptions"/>.</summary>
    public InMemoryProcessedMessageStore(IOptions<KotoWolverineOptions> options)
        => _window = options.Value.IdempotencyWindow;

    /// <inheritdoc/>
    public Task<bool> IsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        if (_processed.TryGetValue(messageId, out var processedAt))
            return Task.FromResult(DateTime.UtcNow - processedAt < _window);

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        _processed[messageId] = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}
