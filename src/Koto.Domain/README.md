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
| `Entity<TId>` | Identity-based equality (transient entities are never equal) |
| `ValueObject` | Component-based equality |
| `StronglyTypedId<T>` | Typed wrappers for Guid / int / string IDs |
| `Result<T>` | Explicit success/failure without exceptions; carries one or many errors (`Errors`) |
| `Result` (static) | `Success()` / `Failure(…)` for void flows, `Combine(…)` to aggregate several results |
| `IResultFactory<TSelf>` | Static-abstract failure factory for generic infrastructure (no reflection) |
| `Error` | Structured error: `Code` + `Message` + optional `Field` |
| `Errors.General` | Shared factory errors (not-found, required, …) |
| `IDomainEvent` / `DomainEvent` | Event interfaces and base record (`init` properties survive JSON round-trips) |
| `Unit` | Return type for commands that produce no value |

> `IRepository<TAgg, TId>` lives in **Koto.Application** (next to `IUnitOfWork`) — the port
> belongs to the layer that consumes it (handlers); the domain stays persistence-free.

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

### Multiple errors and Combine

```csharp
// Aggregate several factory results — ALL errors are collected, not just the first:
var combined = Result.Combine(Email.Create(dto.Email), Name.Create(dto.Name));
if (combined.IsFailure)
    return Result<User>.Failure(combined.Errors);   // every error, each with its own Code

var (email, name) = combined.Value;
```

### Async match

```csharp
// Async success handler + sync failure handler — no Task.FromResult noise:
return await result.MatchAsync(
    onSuccess: async order => await BuildResponseAsync(order, ct),
    onFailure: error => ErrorResponse(error));
```

## License

MIT
