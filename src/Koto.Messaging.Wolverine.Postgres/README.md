# Koto.Messaging.Wolverine.Postgres

Durable PostgreSQL-backed `IProcessedMessageStore` for [Koto.Messaging.Wolverine](https://www.nuget.org/packages/Koto.Messaging.Wolverine): consumer idempotency that survives service restarts.

## Why

`IntegrationEventConsumerBase<TEvent>` deduplicates events via `IProcessedMessageStore`. The default in-memory store loses its state on restart — with Kafka's at-least-once delivery that means real duplicate processing in production. This package stores processed message ids in PostgreSQL instead.

## Usage

```csharp
services.AddKotoWolverine();
services.AddPostgresProcessedMessageStore(
    builder.Configuration.GetConnectionString("db")!);
```

Optional configuration:

```csharp
services.AddPostgresProcessedMessageStore(connectionString, o =>
{
    o.Schema = "koto";                          // default
    o.Table = "processed_messages";             // default
    o.AutoCreateSchema = true;                  // default; disable with external migrations
    o.CleanupInterval = TimeSpan.FromHours(1);  // default
});
```

- The deduplication window comes from `KotoWolverineOptions.IdempotencyWindow` (default 24 h) — the same option the in-memory store uses.
- Schema/table/index are created on first use (`CREATE ... IF NOT EXISTS`); set `AutoCreateSchema = false` to manage them with your migrations:

```sql
CREATE SCHEMA IF NOT EXISTS koto;
CREATE TABLE IF NOT EXISTS koto.processed_messages (
    message_id   uuid PRIMARY KEY,
    processed_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_processed_messages_processed_at
    ON koto.processed_messages (processed_at);
```

- A background service deletes entries older than the idempotency window every `CleanupInterval`.
- Call order relative to `AddKotoWolverine` does not matter.

## Semantics

Delivery stays **at-least-once**: the store marks an event as processed *after* your `ConsumeAsync` completes, outside a shared transaction. A crash in between redelivers the event. For strict business-level deduplication use a deterministic operation id with a unique constraint in the consumer's own storage (see the Koto ledger patterns).

## License

MIT
