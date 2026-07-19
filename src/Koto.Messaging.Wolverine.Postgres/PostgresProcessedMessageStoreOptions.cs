namespace Koto.Messaging.Wolverine.Postgres;

/// <summary>Configuration for <see cref="PostgresProcessedMessageStore"/>.</summary>
public sealed class PostgresProcessedMessageStoreOptions
{
    /// <summary>PostgreSQL schema for the deduplication table. Defaults to <c>koto</c>.</summary>
    public string Schema { get; set; } = "koto";

    /// <summary>Deduplication table name. Defaults to <c>processed_messages</c>.</summary>
    public string Table { get; set; } = "processed_messages";

    /// <summary>
    /// When <c>true</c> (default), the schema, table, and index are created on first use
    /// (<c>CREATE ... IF NOT EXISTS</c>). Disable when migrations are managed externally.
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;

    /// <summary>
    /// Interval between background cleanup runs that delete entries older than
    /// <see cref="KotoWolverineOptions.IdempotencyWindow"/>. Defaults to 1 hour.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
