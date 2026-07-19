using Koto.Messaging.Wolverine.Consuming;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Koto.Messaging.Wolverine.Postgres;

/// <summary>
/// Durable PostgreSQL implementation of <see cref="IProcessedMessageStore"/>.
/// Deduplication state survives service restarts; entries expire after
/// <see cref="KotoWolverineOptions.IdempotencyWindow"/> and are removed by background cleanup.
/// </summary>
/// <remarks>
/// Semantics stay at-least-once: <see cref="MarkAsProcessedAsync"/> runs after the consumer's
/// work, outside a shared transaction. For strict business-level deduplication use a
/// deterministic operation id with a unique constraint in the consumer's own storage.
/// </remarks>
public sealed class PostgresProcessedMessageStore : IProcessedMessageStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresProcessedMessageStoreOptions _options;
    private readonly TimeSpan _window;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>Initializes the store. The data source is owned by the caller (DI container).</summary>
    public PostgresProcessedMessageStore(
        NpgsqlDataSource dataSource,
        IOptions<PostgresProcessedMessageStoreOptions> storeOptions,
        IOptions<KotoWolverineOptions> wolverineOptions)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(storeOptions);
        ArgumentNullException.ThrowIfNull(wolverineOptions);

        _dataSource = dataSource;
        _options = storeOptions.Value;
        _window = wolverineOptions.Value.IdempotencyWindow;
        PostgresIdentifier.Validate(_options.Schema, nameof(_options.Schema));
        PostgresIdentifier.Validate(_options.Table, nameof(_options.Table));
    }

    private string QualifiedTable => $"\"{_options.Schema}\".\"{_options.Table}\"";

    /// <inheritdoc/>
    public async Task<bool> IsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var cmd = _dataSource.CreateCommand(
            $"SELECT 1 FROM {QualifiedTable} WHERE message_id = $1 AND processed_at > now() - $2");
        await using var cmdGuard = cmd.ConfigureAwait(false);
        cmd.Parameters.AddWithValue(messageId);
        cmd.Parameters.AddWithValue(_window);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is not null;
    }

    /// <inheritdoc/>
    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var cmd = _dataSource.CreateCommand(
            $"INSERT INTO {QualifiedTable} (message_id, processed_at) VALUES ($1, now()) " +
            "ON CONFLICT (message_id) DO NOTHING");
        await using var cmdGuard = cmd.ConfigureAwait(false);
        cmd.Parameters.AddWithValue(messageId);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes entries older than <see cref="KotoWolverineOptions.IdempotencyWindow"/>.
    /// Called periodically by the background cleanup service; safe to invoke manually.
    /// </summary>
    /// <returns>The number of deleted rows.</returns>
    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var cmd = _dataSource.CreateCommand(
            $"DELETE FROM {QualifiedTable} WHERE processed_at < now() - $1");
        await using var cmdGuard = cmd.ConfigureAwait(false);
        cmd.Parameters.AddWithValue(_window);

        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaReady)
            return;

        await _schemaLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_schemaReady)
                return;

            if (_options.AutoCreateSchema)
            {
                var cmd = _dataSource.CreateCommand(
                    $"""
                     CREATE SCHEMA IF NOT EXISTS "{_options.Schema}";
                     CREATE TABLE IF NOT EXISTS {QualifiedTable} (
                         message_id uuid PRIMARY KEY,
                         processed_at timestamptz NOT NULL DEFAULT now()
                     );
                     CREATE INDEX IF NOT EXISTS "ix_{_options.Table}_processed_at"
                         ON {QualifiedTable} (processed_at);
                     """);
                await using var cmdGuard = cmd.ConfigureAwait(false);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
