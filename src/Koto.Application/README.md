# Koto.Application

CQRS dispatcher, pipeline behaviors, and cross-service integration abstractions for .NET microservices. Part of the [Koto](https://github.com/arigatory/Koto) suite.

## Install

```bash
dotnet add package Koto.Application
```

## What's included

| Type | Purpose |
|---|---|
| `ICommand` / `ICommand<TResult>` | Marker interfaces for write operations (`ICommandBase`) |
| `IQuery<TResult>` | Marker interface for read operations (`IQueryBase`) |
| `ICommandHandler<T>` / `ICommandHandler<T, TResult>` | Command handler contracts |
| `IQueryHandler<TQuery, TResult>` | Query handler contract |
| `ICqrsDispatcher` | Dispatches commands and queries to registered handlers |
| `IPipelineBehavior<TRequest, TResponse>` | Middleware for the CQRS pipeline |
| `KotoApplicationOptions` | Opt-in behavior registration (`AddLoggingBehavior()`, `AddTransactionBehavior()`, `AddBehavior(type)`) |
| `LoggingBehavior<,>` | Logs every command/query with timing |
| `TransactionBehavior<,>` | Wraps each command in a `IUnitOfWork` transaction |
| `IUnitOfWork` | Abstraction for committing a database transaction |
| `IRepository<TAgg, TId>` | Persistence contract for aggregates (pairs with `IUnitOfWork`) |
| `IIntegrationEvent` / `IntegrationEvent` | External event contract |
| `IIntegrationCommand` / `IIntegrationCommand<TResult>` | Cross-service command contract |
| `IIntegrationEventPublisher` | Publishes integration events (implemented in infra) |
| `IIntegrationCommandDispatcher` | Dispatches integration commands (implemented in infra) |
| `AssemblyScanning` | `GetLoadableTypes` — scan guard against `ReflectionTypeLoadException` |

## Usage

### Register

```csharp
// Handlers only — no pipeline behaviors:
builder.Services.AddKotoApplication(typeof(Program).Assembly);

// With behaviors (opt-in; registration order = execution order, first is outermost):
builder.Services.AddKotoApplication(
    o => o.AddLoggingBehavior().AddTransactionBehavior(),
    typeof(Program).Assembly);
builder.Services.AddKotoValidation(typeof(Program).Assembly); // ValidationBehavior from Koto.Validation
// Recommended order: Logging → Validation → Transaction.
```

### Implement a command

```csharp
public sealed record PlaceOrderCommand(Guid CustomerId, List<OrderItem> Items)
    : ICommand<OrderId>;

public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, OrderId>
{
    public async Task<Result<OrderId>> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        // business logic
    }
}
```

### Dispatch

```csharp
var result = await _dispatcher.SendAsync(new PlaceOrderCommand(customerId, items), ct);
```

### Pipeline behaviors

Behaviors are resolved against the **concrete** command/query type: an open-generic
registration like `ValidationBehavior<,>` closes over `PlaceOrderCommand` at dispatch
time, so `IValidator<PlaceOrderCommand>` is discovered. A behavior can also target a
single request by implementing `IPipelineBehavior<PlaceOrderCommand, Result<OrderId>>`.

```csharp
// Custom open-generic behavior:
builder.Services.AddKotoApplication(
    o => o.AddLoggingBehavior().AddBehavior(typeof(MyMetricsBehavior<,>)),
    typeof(Program).Assembly);
```

## Design notes

- `IDomainEvent` — internal; free to change; never crosses service boundaries.
- `IIntegrationEvent` — external contract; versioned; published to Kafka via Wolverine.
- `IIntegrationCommand` — fire-and-forget command to another service; `IIntegrationCommand<TResult>` expects a reply.

## Pagination

`PagedList<T>` — страница результата с метаданными (`TotalCount`, `TotalPages`, `HasNextPage`). Возвращайте из query-хендлеров: `IQuery<PagedList<OrderDto>>`; материализация из EF Core — `ToPagedListAsync` в `Koto.Infrastructure.EFCore`.
