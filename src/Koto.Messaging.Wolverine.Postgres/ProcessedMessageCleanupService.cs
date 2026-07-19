using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koto.Messaging.Wolverine.Postgres;

/// <summary>
/// Periodically deletes expired deduplication entries via
/// <see cref="PostgresProcessedMessageStore.DeleteExpiredAsync"/>.
/// </summary>
internal sealed class ProcessedMessageCleanupService : BackgroundService
{
    private readonly PostgresProcessedMessageStore _store;
    private readonly TimeSpan _interval;
    private readonly ILogger<ProcessedMessageCleanupService> _logger;

    public ProcessedMessageCleanupService(
        PostgresProcessedMessageStore store,
        IOptions<PostgresProcessedMessageStoreOptions> options,
        ILogger<ProcessedMessageCleanupService> logger)
    {
        _store = store;
        _interval = options.Value.CleanupInterval;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var deleted = await _store.DeleteExpiredAsync(stoppingToken).ConfigureAwait(false);
                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Deleted {Count} expired processed-message entries", deleted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Processed-message cleanup failed; will retry next interval");
            }
        }
    }
}
