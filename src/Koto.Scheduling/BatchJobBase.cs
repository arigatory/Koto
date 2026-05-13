using Microsoft.Extensions.Logging;

namespace Koto.Scheduling;

/// <summary>
/// Abstract base for batch jobs that process a large dataset in pages.
/// Fetches items cursor-by-cursor via <see cref="FetchBatchAsync"/> and processes each
/// via <see cref="ProcessItemAsync"/>. Item-level failures are logged and skipped by default.
/// </summary>
/// <typeparam name="TItem">The type of item being processed.</typeparam>
public abstract class BatchJobBase<TItem> : IScheduledJob
{
    private readonly ILogger _logger;

    /// <summary>Number of items fetched per page. Override to change the batch size.</summary>
    protected virtual int BatchSize => 100;

    /// <inheritdoc/>
    public abstract string JobId { get; }

    /// <summary>Initializes the base with the required logger.</summary>
    protected BatchJobBase(ILogger<BatchJobBase<TItem>> logger) => _logger = logger;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        int offset = 0;
        int totalProcessed = 0;
        int totalFailed = 0;

        while (true)
        {
            var batch = await FetchBatchAsync(offset, BatchSize, ct);
            if (batch.Count == 0) break;

            _logger.LogDebug("Job {JobId} processing batch offset={Offset} count={Count}",
                JobId, offset, batch.Count);

            foreach (var item in batch)
            {
                try
                {
                    await ProcessItemAsync(item, ct);
                    totalProcessed++;
                }
                catch (Exception ex)
                {
                    totalFailed++;
                    _logger.LogError(ex, "Job {JobId} failed to process item at offset {Offset}", JobId, offset);
                }
            }

            if (batch.Count < BatchSize) break;
            offset += batch.Count;
        }

        _logger.LogInformation("Job {JobId} finished: processed={Processed} failed={Failed}",
            JobId, totalProcessed, totalFailed);
    }

    /// <summary>
    /// Fetches the next page of items. Return an empty list to signal that processing is complete.
    /// </summary>
    protected abstract Task<IReadOnlyList<TItem>> FetchBatchAsync(
        int offset, int batchSize, CancellationToken ct);

    /// <summary>Processes a single item. Throw to mark it as failed (the batch continues).</summary>
    protected abstract Task ProcessItemAsync(TItem item, CancellationToken ct);
}
