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
        string.IsNullOrWhiteSpace(value) ? new Errors.General.ValueIsRequired() :
        value.Length > 150              ? new Errors.General.InvalidLength(1, 150) :
                                          new Email(value);
}
```

**Errors that are types — the catalog is the contract:**

```csharp
public sealed record OrderAlreadyCancelled(OrderId OrderId)
    : Error("orders.order.already-cancelled", "The order is already cancelled.");

public sealed record OtpTooManyAttempts(int RetryAfterSeconds)      // RetryAfterSeconds reaches the client as details
    : Error("identity.otp.too-many-attempts", "Too many attempts.");
```

**An aggregate that tells you what happened:**

```csharp
public class Order : AggregateRoot<OrderId>
{
    public static Result<Order> Place(Customer customer, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
            return new Errors.General.CollectionIsTooSmall(1, 0);

        var order = new Order(OrderId.New(), customer.Id);
        order.AddDomainEvent(new OrderPlacedDomainEvent(order.Id, customer.Id));
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

**A handler that enriches explicitly and never drops an error:**

```csharp
public async Task<AppResult<OrderId>> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
{
    var customer = await customers.GetByIdAsync(cmd.CustomerId, ct);
    if (customer is null) return new Errors.General.NotFound("customer", cmd.CustomerId).ForField("customerId");

    var order = Order.Place(customer, cmd.Items);
    if (order.IsFailure) return order.Errors.Enrich();        // all errors, explicitly "nothing to add"

    orders.Add(order.Value);
    return order.Value.Id;
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

**An endpoint that stays out of the way — and one response shape for every transport:**

```csharp
public class PlaceOrderEndpoint : CommandEndpoint<PlaceOrderCommand, OrderId>
{
    public override void Configure() => Post("/orders");
}
```

```json
HTTP 200  { "success": true,  "result": "7c9e…", "errors": [], "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:03Z" }
HTTP 422  { "success": false, "result": null,
            "errors": [ { "code": "identity.otp.too-many-attempts", "message": "Too many attempts.",
                          "field": null, "details": { "retryAfterSeconds": 42 } } ],
            "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:09Z" }
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
| [`Koto.Domain`](https://www.nuget.org/packages/Koto.Domain) | `AggregateRoot`, `ValueObject`, `Result<T>`, the `Error` catalog, strongly-typed IDs |
| [`Koto.Application`](https://www.nuget.org/packages/Koto.Application) | CQRS dispatcher, pipeline behaviors, `AppResult<T>`, `EnrichedError`, `Envelope<T>`, `IRepository`, integration interfaces |
| [`Koto.Validation`](https://www.nuget.org/packages/Koto.Validation) | FluentValidation extensions: `MustBeValueObject`, `AddValidationBehavior` |
| [`Koto.Infrastructure.EFCore`](https://www.nuget.org/packages/Koto.Infrastructure.EFCore) | EF Core base context, generic repository, outbox wiring |
| [`Koto.Infrastructure.Http`](https://www.nuget.org/packages/Koto.Infrastructure.Http) | HTTP client base for calling other services (ACL pattern) |
| [`Koto.EventSourcing.Marten`](https://www.nuget.org/packages/Koto.EventSourcing.Marten) | Event-sourced aggregates on PostgreSQL via Marten |
| [`Koto.Messaging.Wolverine`](https://www.nuget.org/packages/Koto.Messaging.Wolverine) | Integration event/command publisher and consumer bases |
| [`Koto.Messaging.Wolverine.Postgres`](https://www.nuget.org/packages/Koto.Messaging.Wolverine.Postgres) | Durable PostgreSQL idempotency store for consumers |
| [`Koto.Api.AspNetCore`](https://www.nuget.org/packages/Koto.Api.AspNetCore) | `AppResult<T>` → `Envelope<T>` for Minimal API / MVC, coarse status classifier, `EnrichmentGuard` |
| [`Koto.Api.FastEndpoints`](https://www.nuget.org/packages/Koto.Api.FastEndpoints) | Command and query endpoints responding with the envelope, correlation ID, exception handler |
| [`Koto.Observability`](https://www.nuget.org/packages/Koto.Observability) | One-line Serilog + OpenTelemetry setup |
| [`Koto.Scheduling`](https://www.nuget.org/packages/Koto.Scheduling) | Quartz.NET-based scheduled jobs and batch processing |
| [`Koto.Testing`](https://www.nuget.org/packages/Koto.Testing) | Aggregate test fixture, fake repository, `Result`/`AppResult` assertions, `ErrorCatalog` |
| [`Koto.Testing.Integration`](https://www.nuget.org/packages/Koto.Testing.Integration) | Header test auth for WebApplicationFactory, `Eventually` assertions, raw-JSON Kafka test producer |

---

## Getting started

Packages are published to [nuget.org](https://www.nuget.org/profiles/arigatory). v0.5.0-preview.1 is the
first preview of the new error model (see `PLAN.md` for migration notes from 0.4.x):

```bash
dotnet add package Koto.Domain
dotnet add package Koto.Application
dotnet add package Koto.Infrastructure.EFCore
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
builder.Services.AddKotoApi();          // Envelope responses, status classifier, correlation ID
builder.Services.AddFastEndpoints();
builder.Services.AddKotoApplication(    // CQRS handlers + opt-in behaviors (order = execution order)
    o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(),
    typeof(PlaceOrderHandler).Assembly);
builder.Services.AddKotoValidation(typeof(PlaceOrderHandler).Assembly);

var app = builder.Build();
app.UseKotoApi();                       // correlation middleware + global exception handler
app.UseKotoFastEndpoints();             // FastEndpoints with binding errors in the envelope
```

Add `AddKotoValidation(...)`, `AddKotoObservability(...)`, `AddKotoWolverine(...)`, `AddKotoMarten(...)` or `AddKotoScheduling(...)` as your service grows — every package is independent and opt-in.

> `AddKotoEFCore<TContext>` wires the Wolverine outbox and expects Wolverine to be configured. Without Wolverine, register your `KotoDbContext` with plain `AddDbContext` and hand-written repositories — everything else works the same.

---

## Documentation

- Per-package guides: `src/<Package>/README.md` (also shown on each package's nuget.org page)
- Architecture decision records: [`adr/`](adr/) — 26 ADRs covering Result, errors, the envelope, repositories, events, versioning
- Design principles in depth: [`docs/principles/`](docs/principles/) — DDD, errors-and-results, architecture, naming

---

## Design principles

**Always valid.** Domain objects cannot be constructed in an invalid state. Validation lives in factory methods, not in validators.

**Explicit errors.** No exceptions for expected failures. `Result<T>` leaves the domain, `AppResult<T>` leaves the handler, `Envelope<T>` leaves the API — one rail, no exceptions between layers.

**Errors are types.** `Error` is abstract; every error is a record whose properties are the data it guarantees. The catalog is machine-readable, codes are validated, and the compiler enforces the contract where the error is created.

**One response shape.** `{ success, result, errors[{ code, message, field, details }], correlationId, timestamp }` for HTTP, SignalR and Kafka replies alike. HTTP statuses stay meaningful but coarse.

**One source of truth.** Validation rules live once — in the domain. FluentValidation calls them, not duplicates them.

**Events are internal.** Domain events are a private implementation detail. Integration events are the public contract. They are different types, versioned independently.

**No magic.** No runtime reflection on hot paths. No hidden conventions that surprise you at 2am. The code you write is the code that runs.

---

## License

MIT
