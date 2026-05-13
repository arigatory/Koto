namespace Koto.Scheduling;

/// <summary>
/// Marker interface for scheduled jobs managed by <c>Koto.Scheduling</c>.
/// Implement this interface and register via <see cref="ServiceCollectionExtensions.AddKotoScheduling"/>.
/// </summary>
public interface IScheduledJob
{
    /// <summary>
    /// Stable identifier for this job. Used as the Quartz job key.
    /// Convention: <c>"{service}.{job-name}"</c>, e.g. <c>"orders.send-daily-digest"</c>.
    /// </summary>
    string JobId { get; }

    /// <summary>Executes the job logic.</summary>
    Task ExecuteAsync(CancellationToken ct);
}
