# Koto.Infrastructure.EFCore

EF Core 10 integration for the [Koto](https://github.com/arigatory/Koto) DDD suite: generic repository, strongly-typed ID converters, specification pattern, and Wolverine outbox wiring.

## Install

```bash
dotnet add package Koto.Infrastructure.EFCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL  # or your preferred provider
```

## What's included

| Type | Purpose |
|---|---|
| `KotoDbContext` | Abstract DbContext; auto-applies `StronglyTypedIdConvention`; clears domain events after save |
| `Repository<TAgg, TId>` | Generic `IRepository` implementation backed by EF Core |
| `StronglyTypedIdValueConverter<TId, TRaw>` | EF Core `ValueConverter` for `StronglyTypedId<T>` properties |
| `StronglyTypedIdConvention` | Auto-applies converters to all strongly-typed ID properties |
| `ISpecification<T>` / `Specification<T>` | Encapsulates query criteria, includes, and ordering |
| `SpecificationEvaluator` | Applies a specification to an `IQueryable<T>` |
| `AddKotoEFCore<TContext>()` | Registers the DbContext via Wolverine's EF Core integration |

## Usage

### 1 — Define your DbContext

```csharp
public class AppDbContext : KotoDbContext
{
    public AppDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`StronglyTypedIdConvention` is applied automatically — no manual `HasConversion` calls needed.

### 2 — Register

```csharp
// Program.cs
builder.Services.AddKotoEFCore<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Db")));

// Wolverine setup — durable outbox (обязательно для доставки событий)
builder.Host.UseWolverine(opts =>
{
    // Хендлеры вне entry assembly нужно включить в discovery явно:
    opts.Discovery.IncludeAssembly(typeof(SomeHandler).Assembly);

    // Durable outbox: конверты хранятся в Postgres сервиса
    opts.PersistMessagesWithPostgresql(connectionString);   // WolverineFx.Postgresql
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

    opts.UseEntityFrameworkCoreTransactions();

    // Для сохранений ВНУТРИ Wolverine-хендлеров (консюмеры):
    opts.PublishDomainEventsFromEntityFrameworkCore<IHasDomainEvents, IDomainEvent>(
        e => e.DomainEvents);
});
```

### Domain events from plain code (HTTP endpoints)

`PublishDomainEventsFromEntityFrameworkCore` is a codegen policy that only runs **inside
Wolverine handlers**. For saves from ordinary code (FastEndpoints → `ICommandHandler`),
`AddKotoEFCore` registers `EfCoreUnitOfWork<TContext>` as the default `IUnitOfWork`
(`TryAddScoped` — your own registration wins): on `CommitAsync` it collects uncommitted
domain events from tracked aggregates and publishes them through Wolverine's EF Core outbox
in the same transaction as your entity changes:

```csharp
public async Task<Result<OrderId>> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
{
    var order = Order.Place(...);          // AddDomainEvent(...) inside
    _orders.Add(order.Value);
    await _unitOfWork.CommitAsync(ct);     // entities + envelopes in one transaction → handler
    return order.Value.Id;
}
```

### 3 — Use a repository

```csharp
public class OrderRepository : Repository<Order, OrderId>
{
    public OrderRepository(AppDbContext ctx) : base(ctx) { }
}
```

Or rely on the registered `IRepository<Order, OrderId>` directly.

### 4 — Specifications

```csharp
public class ActiveOrdersSpec : Specification<Order>
{
    public ActiveOrdersSpec(CustomerId customerId)
    {
        AddCriteria(o => o.CustomerId == customerId && !o.IsCancelled);
        AddInclude(o => o.Items);
        ApplyOrderBy(o => o.PlacedAt);
    }
}

// In a repository or query handler:
var query = SpecificationEvaluator.GetQuery(_context.Set<Order>(), new ActiveOrdersSpec(customerId));
var orders = await query.ToListAsync(ct);
```

## Pagination

```csharp
var page = await context.Orders
    .Where(o => o.CustomerId == customerId)
    .OrderByDescending(o => o.CreatedAt)
    .ToPagedListAsync(page: 1, pageSize: 20, ct);
```

Запрос должен быть упорядочен — иначе порядок страниц не определён.
