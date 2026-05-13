# Koto.Application

CQRS dispatcher, pipeline behaviors, and cross-service integration abstractions for .NET microservices. Part of the [Koto](https://github.com/arigatory/Koto) suite.

## Install

```bash
dotnet add package Koto.Application
```

## What's included

| Type | Purpose |
|---|---|
| `ICommand` / `ICommand<TResult>` | Marker interfaces for write operations |
| `IQuery<TResult>` | Marker interface for read operations |
| `ICommandHandler<T>` / `ICommandHandler<T, TResult>` | Command handler contracts |
| `IQueryHandler<TQuery, TResult>` | Query handler contract |
| `ICqrsDispatcher` | Dispatches commands and queries to registered handlers |
| `IPipelineBehavior<TRequest, TResponse>` | Middleware for the CQRS pipeline |
| `LoggingBehavior<,>` | Logs every command/query with timing |
| `TransactionBehavior<,>` | Wraps each command in a `IUnitOfWork` transaction |
| `IUnitOfWork` | Abstraction for committing a database transaction |
| `IIntegrationEvent` / `IntegrationEvent` | External event contract |
| `IIntegrationCommand` / `IIntegrationCommand<TResult>` | Cross-service command contract |
| `IIntegrationEventPublisher` | Publishes integration events (implemented in infra) |
| `IIntegrationCommandDispatcher` | Dispatches integration commands (implemented in infra) |

## Usage

### Register

```csharp
builder.Services.AddKotoApplication(typeof(Program).Assembly);
```

### Implement a command

```csharp
public sealed record PlaceOrderCommand(Guid CustomerId, List<OrderItem> Items)
    : ICommand<Result<OrderId>>;

public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, Result<OrderId>>
{
    public async Task<Result<OrderId>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
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

```csharp
// Register in DI — executed in registration order
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)); // from Koto.Validation
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
```

## Design notes

- `IDomainEvent` — internal; free to change; never crosses service boundaries.
- `IIntegrationEvent` — external contract; versioned; published to Kafka via Wolverine.
- `IIntegrationCommand` — fire-and-forget command to another service; `IIntegrationCommand<TResult>` expects a reply.
