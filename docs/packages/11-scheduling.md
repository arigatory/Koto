# Koto.Scheduling — Plan

**Phase:** 4 | **Status:** NOT STARTED
**Depends on:** Koto.Application + Quartz.NET (MIT)

---

## Цель

Scheduled jobs и batch processing с distributed locking — при нескольких запущенных инстансах (HPA) задача выполняется только один раз. Интеграция с Koto.Domain для публикации domain events из jobs.

## Checklist

### Scheduled Job Base
- [ ] `IScheduledJob` — marker interface: `string JobId { get; }`, `Task ExecuteAsync(CancellationToken ct)`
- [ ] `ScheduledJobBase` — abstract base:
  - Structured logging: JobId, start/end, duration, success/failure
  - Publishes `JobStartedDomainEvent`, `JobCompletedDomainEvent`, `JobFailedDomainEvent`
  - Distributed lock via Quartz's clustered mode (одна БД = один экземпляр выполняет)

### Batch Processing
- [ ] `BatchJobBase<TItem>` — base для обработки больших наборов данных:
  - `abstract Task<IReadOnlyList<TItem>> FetchBatchAsync(int offset, int batchSize, CancellationToken ct)`
  - `abstract Task ProcessItemAsync(TItem item, CancellationToken ct)`
  - Cursor-based pagination, configurable batch size
  - Автоматическая обработка ошибок: failed items → DLQ или retry queue
  - Прогресс-трекинг через structured logs

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoScheduling(services, configure)`:
  - Настраивает Quartz.NET с clustered mode (PostgreSQL job store)
  - Регистрирует все `IScheduledJob` из assembly
  - `AddJob<TJob>(cronExpression)` — fluent builder

## Пример использования

```csharp
// Job definition:
public class SendDailyDigestJob : ScheduledJobBase
{
    public override string JobId => "send-daily-digest";

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        var users = await _repo.GetUsersWithPendingDigestAsync(ct);
        foreach (var user in users)
            await _emailService.SendDigestAsync(user, ct);
    }
}

// Registration:
builder.Services.AddKotoScheduling(quartz =>
{
    quartz.AddJob<SendDailyDigestJob>(cron: "0 0 8 * * ?"); // 8:00 every day
    quartz.AddJob<CleanupExpiredTokensJob>(cron: "0 */15 * * * ?"); // every 15 min
});
```

## K8s note

При HPA (несколько pod) Quartz clustered mode гарантирует, что job выполняется только одним pod через database-level locking. Не нужен дополнительный distributed lock.

## Тесты
- [ ] Job выполняется только один раз при нескольких инстансах (integration test с двумя hosts)
- [ ] Failure в job логируется и не роняет хост
- [ ] BatchJobBase обрабатывает все страницы, продолжает при ошибке одного item
