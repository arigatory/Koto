# Koto.EventSourcing.Marten

Event Sourcing on PostgreSQL via [Marten](https://martendb.io). Part of the [Koto](https://github.com/arigatory/Koto) suite.

## Install

```bash
dotnet add package Koto.EventSourcing.Marten
```

## What's included

| Type | Purpose |
|---|---|
| `EventSourcedAggregateRoot<TId>` | Base class with `RaiseEvent`, `Apply`, and `Reconstitute` |
| `IEventSourcedRepository<TAgg, TId>` | Persistence contract for event-sourced aggregates |
| `MartenEventSourcedRepository<TAgg, TId>` | Marten-backed implementation |
| `InlineProjection<TReadModel, TId>` | Base for inline single-stream projections |
| `AsyncProjection<TReadModel, TId>` | Base for async daemon single-stream projections |
| `AddKotoMarten()` | Registers Marten and the generic repository |

## Usage

### 1 — Define an event-sourced aggregate

```csharp
public sealed class Order : EventSourcedAggregateRoot<OrderId>
{
    public decimal Total { get; private set; }
    public bool IsShipped { get; private set; }

    public static Order Place(OrderId id, decimal total)
    {
        var order = new Order();
        order.RaiseEvent(new OrderPlaced(Guid.NewGuid(), DateTime.UtcNow, id, total));
        return order;
    }

    public void Ship() => RaiseEvent(new OrderShipped(Guid.NewGuid(), DateTime.UtcNow));

    protected override void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderPlaced e: Total = e.Total; break;
            case OrderShipped:  IsShipped = true; break;
        }
    }

    private Order() { }
}
```

### 2 — Register

```csharp
builder.Services.AddKotoMarten(
    builder.Configuration.GetConnectionString("Db")!,
    opts =>
    {
        // optional extra Marten config
        opts.Projections.Add<OrderSummaryProjection>(ProjectionLifecycle.Async);
    });
```

### 3 — Use the repository

```csharp
public class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, Result<OrderId>>
{
    private readonly IEventSourcedRepository<Order, OrderId> _orders;

    public async Task<Result<OrderId>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Place(OrderId.New(), cmd.Total);
        await _orders.SaveAsync(order, ct);
        return order.Id;
    }
}
```

### 4 — Write a projection

```csharp
public sealed class OrderSummaryProjection : AsyncProjection<OrderSummary, Guid>
{
    public OrderSummary Create(OrderPlaced e) => new(e.OrderId, e.Total, false);
    public OrderSummary Apply(OrderShipped _, OrderSummary current) =>
        current with { IsShipped = true };
}
```

## Using Marten alongside EF Core

Marten and `Koto.Infrastructure.EFCore` can share the same PostgreSQL instance. Marten manages event streams (`mt_events`, `mt_streams`); EF Core manages read models and other entities.
