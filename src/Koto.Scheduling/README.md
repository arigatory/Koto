# Koto.Scheduling

Quartz.NET-based scheduled jobs and batch processing for Koto microservices.

## What's included

| Type | Purpose |
|---|---|
| `IScheduledJob` | Marker interface: `JobId` + `ExecuteAsync` |
| `ScheduledJobBase` | Quartz `IJob` wrapper with structured logging and error handling |
| `BatchJobBase<TItem>` | Cursor-based batch processor: fetch → process page by page |
| `AddKotoScheduling(builder => ...)` | One-liner DI registration |

## Setup

```csharp
// Program.cs
builder.Services.AddKotoScheduling(scheduling =>
{
    scheduling.AddJob<SendDailyDigestJob>("0 0 8 * * ?");  // 08:00 every day
    scheduling.AddJob<CleanupExpiredTokensJob>("0 */15 * * * ?"); // every 15 min

    // For HPA / multi-pod: enable PostgreSQL clustered job store
    scheduling.UseJobStore(builder.Configuration.GetConnectionString("QuartzDb")!);
});
```

## Implementing a scheduled job

```csharp
public class SendDailyDigestJob : ScheduledJobBase
{
    private readonly IUserRepository _users;
    private readonly IEmailService _email;

    public override string JobId => "orders.send-daily-digest";

    public SendDailyDigestJob(
        IUserRepository users,
        IEmailService email,
        ILogger<ScheduledJobBase> logger) : base(logger)
    {
        _users = users;
        _email = email;
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        var users = await _users.GetUsersWithPendingDigestAsync(ct);
        foreach (var user in users)
            await _email.SendDigestAsync(user, ct);
    }
}
```

## Implementing a batch job

```csharp
public class SyncProductsJob : BatchJobBase<ProductDto>
{
    public override string JobId => "catalog.sync-products";

    protected override int BatchSize => 50;

    public SyncProductsJob(ILogger<BatchJobBase<ProductDto>> logger) : base(logger) { }

    protected override Task<IReadOnlyList<ProductDto>> FetchBatchAsync(
        int offset, int batchSize, CancellationToken ct)
        => _api.GetProductsAsync(offset, batchSize, ct);

    protected override Task ProcessItemAsync(ProductDto item, CancellationToken ct)
        => _repo.UpsertAsync(item, ct);
}
```

## Distributed locking (HPA / multi-pod)

Call `.UseJobStore(connectionString)` to switch Quartz to PostgreSQL-backed clustered mode.
Only one pod executes each trigger at a time — no additional distributed lock needed.
Requires the [Quartz PostgreSQL schema](https://github.com/quartznet/quartznet/tree/main/database/tables)
to be applied to the database.
