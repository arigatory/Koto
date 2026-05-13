using Microsoft.Extensions.Logging;
using Quartz;

namespace Koto.Scheduling;

/// <summary>
/// Abstract base for scheduled jobs. Wraps <see cref="ExecuteAsync"/> with structured logging
/// (JobId, duration, success/failure) and ensures exceptions don't crash the Quartz scheduler.
/// Subclass this and implement <see cref="ExecuteAsync"/>.
/// </summary>
[DisallowConcurrentExecution]
public abstract class ScheduledJobBase : IJob, IScheduledJob
{
    private readonly ILogger _logger;

    /// <inheritdoc/>
    public abstract string JobId { get; }

    /// <summary>Initializes the base with the required logger.</summary>
    protected ScheduledJobBase(ILogger<ScheduledJobBase> logger) => _logger = logger;

    /// <inheritdoc/>
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var started = DateTime.UtcNow;

        _logger.LogInformation("Job {JobId} started", JobId);

        try
        {
            await ExecuteAsync(ct);
            var elapsed = DateTime.UtcNow - started;
            _logger.LogInformation("Job {JobId} completed in {ElapsedMs}ms", JobId, elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Job {JobId} was cancelled", JobId);
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - started;
            _logger.LogError(ex, "Job {JobId} failed after {ElapsedMs}ms", JobId, elapsed.TotalMilliseconds);
            // Don't re-throw: Quartz will mark the job as failed and reschedule per policy.
            // Throwing JobExecutionException with refireImmediately: false is the Quartz idiom.
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    /// <summary>Implement the job logic here.</summary>
    public abstract Task ExecuteAsync(CancellationToken ct);
}
