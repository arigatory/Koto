# Koto.Messaging.Wolverine.Postgres

Durable PostgreSQL-реализация `IProcessedMessageStore` для идемпотентных консюмеров.

## Goals

- Дедупликация сообщений, переживающая рестарт сервиса: `InMemoryProcessedMessageStore` теряет состояние, что на проде превращает at-least-once доставку Kafka в реальные дубли обработки.
- Zero-ceremony: одна строка DI, схема создаётся автоматически (opt-out).
- Автоочистка устаревших записей (за пределами `IdempotencyWindow`) — таблица не растёт бесконечно.

## Non-goals

- Транзакционная атомарность «обработал + пометил» с бизнес-данными консюмера — семантика остаётся at-least-once с окном дублирования между `ConsumeAsync` и `MarkAsProcessedAsync` (как и у базового контракта). Строгий exactly-once дают паттерны уровня приложения (детерминированный OperationId + unique constraint, как в ledger-сценариях).
- Поддержка других СУБД (Redis, SQL Server) — отдельные пакеты при необходимости.

## Public API

```csharp
// DI (вызывать после AddKotoWolverine; порядок не важен — базовая регистрация TryAdd)
services.AddPostgresProcessedMessageStore(
    connectionString,
    o =>
    {
        o.Schema = "koto";                       // default
        o.Table = "processed_messages";          // default
        o.AutoCreateSchema = true;               // default
        o.CleanupInterval = TimeSpan.FromHours(1); // default
    });

public sealed class PostgresProcessedMessageStoreOptions
{
    public string Schema { get; set; } = "koto";
    public string Table { get; set; } = "processed_messages";
    public bool AutoCreateSchema { get; set; } = true;
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}

public sealed class PostgresProcessedMessageStore : IProcessedMessageStore
{
    Task<bool> IsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task<int> DeleteExpiredAsync(CancellationToken ct = default); // используется фоновой очисткой, доступен и вручную
}
```

- Окно дедупликации — из `KotoWolverineOptions.IdempotencyWindow` (единый источник, как у in-memory реализации).
- Схема: `{schema}.{table} (message_id uuid PRIMARY KEY, processed_at timestamptz NOT NULL DEFAULT now())` + индекс по `processed_at`.
- `MarkAsProcessedAsync` — `INSERT … ON CONFLICT DO NOTHING` (безопасен при гонках).
- Имена схемы/таблицы валидируются (`^[a-z_][a-z0-9_]*$`) при регистрации — защита от SQL-инъекции через конфигурацию.
- Фоновая очистка — `BackgroundService` c `PeriodicTimer`, удаляет `processed_at < now() - window`.

## Dependencies

- `Npgsql` (PostgreSQL License, permissive — ок)
- `Microsoft.Extensions.Hosting.Abstractions` (BackgroundService), `Options`, `Logging.Abstractions`, `DependencyInjection.Abstractions` (MIT)
- ProjectReference: `Koto.Messaging.Wolverine`

## Resolved questions

- **Отдельный пакет, а не зависимость в `Koto.Messaging.Wolverine`** — см. ADR-021.
- **NpgsqlDataSource не регистрируется в DI под своим типом** — оборачивается во внутренний holder, чтобы не конфликтовать с data source приложения (EF/Marten).
