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
| [`Koto.Domain`](https://www.nuget.org/packages/Koto.Domain) | `AggregateRoot`, `ValueObject`, `Result<T>`, `Error`, strongly-typed IDs |
| [`Koto.Application`](https://www.nuget.org/packages/Koto.Application) | CQRS dispatcher, pipeline behaviors, `IRepository`, integration interfaces |
| [`Koto.Validation`](https://www.nuget.org/packages/Koto.Validation) | FluentValidation extensions: `MustBeValueObject`, `MustBeEntity` |
| [`Koto.Infrastructure.EFCore`](https://www.nuget.org/packages/Koto.Infrastructure.EFCore) | EF Core base context, generic repository, outbox wiring |
| [`Koto.Infrastructure.Http`](https://www.nuget.org/packages/Koto.Infrastructure.Http) | HTTP client base for calling other services (ACL pattern) |
| [`Koto.EventSourcing.Marten`](https://www.nuget.org/packages/Koto.EventSourcing.Marten) | Event-sourced aggregates on PostgreSQL via Marten |
| [`Koto.Messaging.Wolverine`](https://www.nuget.org/packages/Koto.Messaging.Wolverine) | Integration event/command publisher and consumer bases |
| [`Koto.Api.AspNetCore`](https://www.nuget.org/packages/Koto.Api.AspNetCore) | Transport-agnostic `Result<T>` → HTTP: Minimal API / MVC mapping, RFC 7807 Problem Details, `KotoHttpErrorOptions` |
| [`Koto.Api.FastEndpoints`](https://www.nuget.org/packages/Koto.Api.FastEndpoints) | Command and query endpoints, Problem Details, correlation ID |
| [`Koto.Observability`](https://www.nuget.org/packages/Koto.Observability) | One-line Serilog + OpenTelemetry setup |
| [`Koto.Scheduling`](https://www.nuget.org/packages/Koto.Scheduling) | Quartz.NET-based scheduled jobs and batch processing |
| [`Koto.Testing`](https://www.nuget.org/packages/Koto.Testing) | Aggregate test fixture, fake repository, integration test base |

---

## Getting started

Packages are published to [nuget.org](https://www.nuget.org/profiles/arigatory) as pre-releases:

```bash
dotnet add package Koto.Domain --prerelease
dotnet add package Koto.Application --prerelease
dotnet add package Koto.Infrastructure.EFCore --prerelease
```

### Scaffold a service by hand

The typical clean-architecture layout is four projects, each referencing one slice of Koto:

| Project | Koto packages |
|---|---|
| `MyService.Domain` | `Koto.Domain` |
| `MyService.Application` | `Koto.Application`, `Koto.Validation` |
| `MyService.Infrastructure` | `Koto.Infrastructure.EFCore` |
| `MyService.Api` | `Koto.Api.FastEndpoints`, `Koto.Observability` |

Wire it up in `Program.cs`:

```csharp
builder.Services.AddKotoApi();          // Result→HTTP, Problem Details, correlation ID
builder.Services.AddFastEndpoints();
builder.Services.AddKotoApplication(    // CQRS handlers + opt-in behaviors
    typeof(PlaceOrderHandler).Assembly);

var app = builder.Build();
app.UseKotoApi();                       // correlation middleware + global exception handler
app.UseFastEndpoints();
```

Add `AddKotoValidation(...)`, `AddKotoObservability(...)`, `AddKotoWolverine(...)`, `AddKotoMarten(...)` or `AddKotoScheduling(...)` as your service grows — every package is independent and opt-in.

> `AddKotoEFCore<TContext>` wires the Wolverine outbox and expects Wolverine to be configured. Without Wolverine, register your `KotoDbContext` with plain `AddDbContext` and hand-written repositories — everything else works the same.

---

## Documentation

- Per-package guides: `src/<Package>/README.md` (also shown on each package's nuget.org page)
- Architecture decision records: [`adr/`](adr/) — 20 ADRs covering Result, repositories, events, versioning
- Design principles in depth: [`docs/principles/`](docs/principles/) — DDD, errors-and-results, architecture, naming

---

## Design principles

**Always valid.** Domain objects cannot be constructed in an invalid state. Validation lives in factory methods, not in validators.

**Explicit errors.** No exceptions for expected failures. `Result<T>` flows through the entire stack — from domain to HTTP response.

**One source of truth.** Validation rules live once — in the domain. FluentValidation calls them, not duplicates them.

**Events are internal.** Domain events are a private implementation detail. Integration events are the public contract. They are different types, versioned independently.

**No magic.** No runtime reflection on hot paths. No hidden conventions that surprise you at 2am. The code you write is the code that runs.

---

## License

MIT
