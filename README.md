# Koto

Composable .NET building blocks for microservices that take domain modelling seriously.

---

## What it is

Koto is a set of small, focused NuGet packages that wire together the best free .NET libraries — Wolverine, Marten, FastEndpoints, EF Core — with opinionated conventions for DDD, CQRS, and Event Sourcing. Use what you need, leave the rest.

## What it looks like

**A domain model that can't be created in an invalid state:**

```csharp
public sealed record Email
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static Result<Email> Create(string value) =>
        string.IsNullOrWhiteSpace(value) ? Errors.General.ValueIsRequired() :
        value.Length > 150              ? Errors.General.InvalidLength(1, 150) :
                                          new Email(value);
}
```

**An aggregate that tells you what happened:**

```csharp
public class Order : AggregateRoot<OrderId>
{
    public static Result<Order> Place(Customer customer, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
            return OrderErrors.NoItems();

        var order = new Order(OrderId.New(), customer.Id);
        order.AddDomainEvent(new OrderPlacedDomainEvent(order.Id, customer.Id));
        return order;
    }

    public Result<Unit> Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            return OrderErrors.AlreadyCancelled();

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledDomainEvent(Id, reason));
        return Unit.Value;
    }
}
```

**Validation that delegates to the domain:**

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.Email).MustBeValueObject(Email.Create);
        RuleFor(x => x.Items).ListMustContainNumberOfItems(min: 1, max: 50);
    }
}
```

**An endpoint that stays out of the way:**

```csharp
public class PlaceOrderEndpoint : CommandEndpoint<PlaceOrderCommand, OrderId>
{
    public override void Configure() => Post("/orders");
}
```

**Events that flow from domain to Kafka automatically:**

```
Order.Cancel()
  → AddDomainEvent(OrderCancelledDomainEvent)
  → DbContext.SaveChangesAsync()            // domain event stored in outbox, same transaction
  → Wolverine delivers to in-process handler
  → handler publishes OrderCancelledIntegrationEvent
  → Kafka → other services
```

---

## Packages

| Package | Purpose |
|---|---|
| `Koto.Domain` | `AggregateRoot`, `ValueObject`, `Result<T>`, `Error`, `IRepository` |
| `Koto.Application` | CQRS dispatcher, pipeline behaviors, integration interfaces |
| `Koto.Validation` | FluentValidation extensions: `MustBeValueObject`, `MustBeEntity` |
| `Koto.Infrastructure.EFCore` | EF Core base context, generic repository, outbox wiring |
| `Koto.Infrastructure.Http` | HTTP client base for calling other services (ACL pattern) |
| `Koto.EventSourcing.Marten` | Event-sourced aggregates on PostgreSQL via Marten |
| `Koto.Messaging.Wolverine` | Integration event/command publisher and consumer bases |
| `Koto.Api.FastEndpoints` | Command and query endpoints, Problem Details, correlation ID |
| `Koto.Observability` | One-line Serilog + OpenTelemetry setup |
| `Koto.Testing` | Aggregate test fixture, fake repository, integration test base |

---

## Getting started

```bash
dotnet add package Koto.Domain
dotnet add package Koto.Application
dotnet add package Koto.Infrastructure.EFCore
```

Or scaffold a full microservice:

```bash
dotnet new install Koto.Templates
dotnet new koto-microservice --name OrderService --arch clean
```

---

## Design principles

**Always valid.** Domain objects cannot be constructed in an invalid state. Validation lives in factory methods, not in validators.

**Explicit errors.** No exceptions for expected failures. `Result<T, Error>` flows through the entire stack — from domain to HTTP response.

**One source of truth.** Validation rules live once — in the domain. FluentValidation calls them, not duplicates them.

**Events are internal.** Domain events are a private implementation detail. Integration events are the public contract. They are different types, versioned independently.

**No magic.** No runtime reflection on hot paths. No hidden conventions that surprise you at 2am. The code you write is the code that runs.

---

## License

MIT
