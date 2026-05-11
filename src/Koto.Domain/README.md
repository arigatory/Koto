# Koto.Domain

DDD building blocks for .NET microservices. Zero external dependencies.

## Install

```bash
dotnet add package Koto.Domain
```

## What's included

| Type | Purpose |
|---|---|
| `AggregateRoot<TId>` | Base class that collects domain events |
| `Entity<TId>` | Identity-based equality |
| `ValueObject` | Component-based equality |
| `StronglyTypedId<T>` | Typed wrappers for Guid / int / string IDs |
| `Result<T>` | Explicit success/failure without exceptions |
| `Error` | Structured error: `Code` + `Message` |
| `Errors.General` | Shared factory errors (not-found, required, …) |
| `IRepository<TAgg, TId>` | Persistence contract for aggregates |
| `IDomainEvent` / `DomainEvent` | Event interfaces and base record |
| `Unit` | Return type for commands that produce no value |

## Usage

### Value Object

```csharp
public sealed class Email : ValueObject
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static Result<Email> Create(string value) =>
        string.IsNullOrWhiteSpace(value) ? Errors.General.ValueIsRequired() :
        value.Length > 150              ? Errors.General.InvalidLength(1, 150) :
                                          new Email(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }
}
```

### Aggregate Root

```csharp
public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value);

public class Order : AggregateRoot<OrderId>
{
    public static Result<Order> Place(IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
            return Errors.General.CollectionIsTooSmall(1, 0);

        var order = new Order(new OrderId(Guid.NewGuid()));
        order.AddDomainEvent(new OrderPlacedEvent(order.Id));
        return order;
    }

    public Result<Unit> Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            return new Error("orders.already-cancelled", "Order is already cancelled.");

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(Id, reason));
        return Unit.Value;
    }
}
```

### Result chaining

```csharp
Result<OrderId> result = await ValidateAsync(request)
    .BindAsync(dto => Order.Place(dto.Items))
    .TapAsync(order => repository.Add(order))
    .MapAsync(order => order.Id);
```

### Repository

```csharp
public interface IOrderRepository : IRepository<Order, OrderId> { }
```

## License

MIT
