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
| `Error` | **Abstract** record — every error is a record type from a catalog; its properties are the data the error guarantees |
| `ErrorCode` | Validates codes: `context.entity.problem`, at least three kebab-case segments |
| `Errors.General` | Shared catalog: `ValueIsRequired`, `InvalidLength`, `NotFound`, `CollectionIsTooSmall`, `CollectionIsTooLarge` |
| `Result<T>` | A value or an `ErrorList<Error>`; `FirstError` is the headline, `Errors` is the whole list |
| `ErrorList<TError>` | Immutable error list that converts implicitly into a result: `return other.Errors;` |
| `Result` (static) | `Success()` / `Failure(…)` for void flows, `Combine`, `Sequence`, `Traverse` |
| `IDomainEvent` / `DomainEvent` | Event interfaces and base record (`init` properties survive JSON round-trips) |
| `Unit` | Return type for commands that produce no value |

> `IRepository<TAgg, TId>` lives in **Koto.Application** (next to `IUnitOfWork`) — the port
> belongs to the layer that consumes it (handlers); the domain stays persistence-free.

## Errors are types

```csharp
// Identity.Domain/Errors/OtpErrors.cs — the catalog reads as the contract
public sealed record OtpExpired(DateTimeOffset ExpiredAt)
    : Error("identity.otp.expired", "The code has expired.");

public sealed record OtpTooManyAttempts(int RetryAfterSeconds)
    : Error("identity.otp.too-many-attempts", "Too many attempts. Try again later.");
```

- There is no `new Error(code, message)`: `Error` is abstract. Define the record in the layer that
  detects the condition — domain, application or infrastructure.
- The record's properties are the error's guaranteed data. They reach clients as `details`
  (`{ "retryAfterSeconds": 42 }`), and C# code can pattern-match on the type.
- Codes are validated on construction (`ErrorCode.Pattern`): `orders.order.not-found`,
  `general.value.is-required`. `Koto.Testing`'s `ErrorCatalog.ShouldBeValid(assembly)` checks the
  format and the uniqueness of codes across the whole catalog.
- Shared errors are created the same way: `new Errors.General.NotFound("order", id)`.

## Usage

### Value Object

```csharp
public sealed record Email
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static Result<Email> Create(string value) =>
        string.IsNullOrWhiteSpace(value) ? new Errors.General.ValueIsRequired() :
        value.Length > 150              ? new Errors.General.InvalidLength(1, 150) :
                                          new Email(value);
}
```

### Aggregate Root

```csharp
public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}

public sealed record OrderAlreadyCancelled(OrderId OrderId)
    : Error("orders.order.already-cancelled", "The order is already cancelled.");

public class Order : AggregateRoot<OrderId>
{
    public static Result<Order> Place(IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
            return new Errors.General.CollectionIsTooSmall(1, 0);

        var order = new Order(OrderId.New());
        order.AddDomainEvent(new OrderPlacedDomainEvent(order.Id));
        return order;
    }

    public Result<Unit> Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            return new OrderAlreadyCancelled(Id);

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledDomainEvent(Id, reason));
        return Unit.Value;
    }
}
```

### Propagate all errors, not the first

```csharp
var email = Email.Create(dto.Email);
if (email.IsFailure)
    return email.Errors;        // every error travels on — as short as it gets

// Not this: `return email.FirstError;` keeps one error of N. FirstError is for display and
// for the HTTP status, not for propagation.
```

### Combine several factories (all errors are collected)

```csharp
var fields = Result.Combine(Email.Create(dto.Email), Name.Create(dto.Name));
if (fields.IsFailure)
    return fields.Errors;

var (email, name) = fields.Value;
```

### Fold collections

```csharp
Result<IReadOnlyList<Money>> prices = Result.Traverse(lines, l => Money.Create(l.Amount));
var jumps = await Result.TraverseAsync(request.Jumps, ResolveJumpAsync, FailureMode.FailFast, ct);
```

### Match

```csharp
return result.Match(
    onSuccess: order => Ok(order),
    onFailure: errors => Problem(errors[0].Message, errors.Count));
```

## Upgrading from 0.4

0.5 makes `Error` abstract — every error is a record in a catalog — and renames `Result<T>.Error` to `FirstError`; `Errors` is an `ErrorList<Error>`, `TapError`, `IDomainService` and `IResultFactory` are gone, the `Errors.General` codes changed. The step-by-step guide for the whole suite, with before/after code, is
[docs/migrations/0.4-to-0.5.md](https://github.com/arigatory/Koto/blob/main/docs/migrations/0.4-to-0.5.md).

## License

MIT
