# Building a .NET service with Koto

Exact signatures can shift between minor releases (Koto is pre-1.0; the current line is **0.5.0-preview.1**) — before writing code against a package, skim its README (`src/<Package>/README.md` in the Koto repo, or the nuget.org package page). The error model (records, `AppResult`, envelope, migration from 0.4) has its own reference: [error-model.md](error-model.md).

## Package selection

| Layer / need | Package |
|---|---|
| Domain model (always) | `Koto.Domain` — zero deps: `AggregateRoot<TId>`, `Entity<TId>`, `ValueObject`, `StronglyTypedId<T>`, `Result<T>`, abstract `Error` (catalog of records), `ErrorCode`, `Errors.General`, `IDomainEvent`, `Unit` |
| Application layer (always) | `Koto.Application` — `ICommand`/`IQuery` + handlers, `ICqrsDispatcher`, behaviors, `AppResult<T>`, `EnrichedError`, `Envelope<T>`, `IRepository`, `IUnitOfWork`, `PagedList<T>`, integration contracts |
| Request validation | `Koto.Validation` — FluentValidation v12 extensions (`MustBeValueObject`, `ListMustContainNumberOfItems`), `ValidationBehavior`, `AddValidationBehavior()`, `AddKotoValidation(asm)` |
| State-based persistence | `Koto.Infrastructure.EFCore` — `KotoDbContext`, generic `Repository<TAgg,TId>`, `EfCoreUnitOfWork<TContext>`, strongly-typed-id conventions, `ToPagedListAsync`, Wolverine outbox wiring (`AddKotoEFCore<TContext>`) |
| Event sourcing | `Koto.EventSourcing.Marten` — `EventSourcedAggregateRoot<TId>`, projections (PostgreSQL/Marten 9) |
| HTTP API (FastEndpoints) | `Koto.Api.FastEndpoints` — `CommandEndpoint`/`QueryEndpoint`/`Mapped*`, `AddKotoApi`/`UseKotoApi`/`UseKotoFastEndpoints`, correlation, exception handler; pulls in `Koto.Api.AspNetCore` and `Koto.Validation` |
| HTTP API (Minimal API / MVC, no FastEndpoints) | `Koto.Api.AspNetCore` — `AddKotoAspNetCore`, `ToHttpResult()` / `ToActionResult()` over `AppResult<T>` → `Envelope<T>`, `KotoApiOptions`, `EnrichmentGuard` |
| Kafka messaging | `Koto.Messaging.Wolverine` — publishers/consumers, idempotency + correlation middleware |
| Durable consumer idempotency (PostgreSQL) | `Koto.Messaging.Wolverine.Postgres` — `AddPostgresProcessedMessageStore(connString)`: dedup survives restarts, auto schema, background cleanup |
| Calling other services | `Koto.Infrastructure.Http` — `ServiceHttpClient` (envelope → `Result<T>`, ACL pattern), `RemoteError`, retry/circuit-breaker, `X-Service-Key` auth |
| Logs/traces/metrics | `Koto.Observability` — `AddKotoObservability` (Serilog + OpenTelemetry) |
| Cron/batch jobs | `Koto.Scheduling` — Quartz: `ScheduledJobBase`, `BatchJobBase<TItem>` |
| Tests (domain/unit) | `Koto.Testing` — `AggregateTestFixture`, `FakeRepository`, `Result`/`AppResult` assertions (AwesomeAssertions), `ErrorCatalog` |
| Tests (integration) | `Koto.Testing.Integration` — `HeaderTestAuthHandler` (+`AddHeaderTestAuthentication`/`WithTestUser`), `Eventually.AssertAsync`, `RawJsonKafkaProducer` (topic from the `Topic` const via `IntegrationEventTopics`); composes with Testcontainers/WebApplicationFactory, no container deps of its own |

## Namespaces (`using` lines agents guess wrong)

Most packages put everything in the namespace equal to the package id; the exceptions are listed in full.

| `using` | Brings |
|---|---|
| `Koto.Domain` | all of Koto.Domain: `AggregateRoot<TId>`, `Entity<TId>`, `ValueObject`, `StronglyTypedId<T>`, `Result<T>`, `Result`, `Error`, `Errors`, `ErrorList<T>`, `Unit`, `DomainEvent`, `IDomainEvent`, `IHasDomainEvents` |
| `Koto.Application` | all of Koto.Application: `ICommand`/`IQuery` + handlers, `ICqrsDispatcher`, `AppResult<T>`, `EnrichedError`, the `ForField`/`Enrich`/`WithDetails` verbs, `Envelope<T>`, `IRepository<,>`, `IUnitOfWork`, `PagedList<T>`, `KotoApplicationOptions`, `AddKotoApplication`, integration contracts |
| `Koto.Validation` | `MustBeValueObject`, `ListMustContainNumberOfItems`, `AddValidationBehavior`, `AddKotoValidation` |
| `Koto.Infrastructure.EFCore` | `KotoDbContext`, `Repository<TAgg,TId>`, `EfCoreUnitOfWork<TContext>`, `AddKotoEFCore`, `ToPagedListAsync` |
| `Koto.Api.FastEndpoints` | `AddKotoApi`, `UseKotoApi`, `UseKotoFastEndpoints`, `MapKotoFastEndpoints` |
| `Koto.Api.FastEndpoints.Endpoints` | **`CommandEndpoint<…>`, `QueryEndpoint<…>`, `MappedCommandEndpoint<…>`, `MappedQueryEndpoint<…>`** — every endpoint file needs this one |
| `Koto.Api.FastEndpoints.Extensions` | `User.GetUserId()` and the other `ClaimsPrincipal` helpers |
| `Koto.Api.FastEndpoints.Middleware` | `ICorrelationIdAccessor`, `CorrelationContext`, `GlobalExceptionHandler` |
| `Koto.Api.AspNetCore` | `AddKotoAspNetCore`, `ToHttpResult`/`ToActionResult`, `KotoApiOptions`, `EnrichmentGuardMode` |
| `Koto.Infrastructure.Http` | `ServiceHttpClient`, `RemoteError`, `HttpErrors`, `AddServiceHttpClient`, `AddServiceKey` |
| `Koto.Messaging.Wolverine` · `.Consuming` · `.Publishing` | `UseKotoKafka`, `KotoWolverineOptions` · `IntegrationEventConsumerBase<T>`, `IntegrationCommandConsumerBase<T>`, `IProcessedMessageStore` · the Wolverine publisher/dispatcher |
| `Koto.Messaging.Wolverine.Postgres` | `AddPostgresProcessedMessageStore` |
| `Koto.EventSourcing.Marten` · `.Projections` | `EventSourcedAggregateRoot<TId>`, `IEventSourcedRepository<,>`, `AddKotoMarten` · `InlineProjection<,>`, `AsyncProjection<,>` |
| `Koto.Observability` | `AddKotoObservability` |
| `Koto.Scheduling` | `ScheduledJobBase`, `BatchJobBase<TItem>`, `AddKotoScheduling` |
| `Koto.Testing.Assertions` · `.Catalog` · `.Aggregates` · `.Fakes` | `Should()` for `Result`/`AppResult` · `ErrorCatalog` · `AggregateTestFixture<TAgg>` · `FakeRepository<,>`, `FakeIntegrationEventPublisher` |
| `Koto.Testing.Integration` | `HeaderTestAuthHandler`, `AddHeaderTestAuthentication`, `WithTestUser`, `Eventually`, `RawJsonKafkaProducer` |

## Scaffold recipe (clean architecture, 4 projects)

There is no `dotnet new` template — scaffold by hand.

```
MyService.Domain          → Koto.Domain
MyService.Application     → Koto.Application, Koto.Validation   (+ ProjectRef: Domain)
MyService.Infrastructure  → Koto.Infrastructure.EFCore, Npgsql.EntityFrameworkCore.PostgreSQL   (+ ProjectRef: Application)
MyService.Api             → Koto.Api.FastEndpoints, FastEndpoints, Koto.Observability   (+ ProjectRef: Infrastructure)
MyService.Domain.Tests    → Koto.Testing, xunit   (+ ProjectRef: Domain, Application)
```

All Koto packages: `dotnet add package <Name> --prerelease` (0.5 is a preview line; keep one version across projects, e.g. `<PackageReference Include="Koto.Domain" Version="0.5.0-preview.1" />`). `net10.0`, nullable and implicit usings on.

### Domain

```csharp
public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}

public sealed record Email
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static Result<Email> Create(string value) =>
        string.IsNullOrWhiteSpace(value) ? new Errors.General.ValueIsRequired() :
        value.Length > 150              ? new Errors.General.InvalidLength(1, 150) :
                                          new Email(value);
}

public sealed record OrderHasNoItems() : Error("orders.order.no-items", "Order must contain at least one item.");

public sealed record OrderPlacedDomainEvent(OrderId OrderId, CustomerId CustomerId) : DomainEvent;   // base record adds EventId, OccurredAt

public class Order : AggregateRoot<OrderId>
{
    private Order() { }                                                                  // EF Core materialization (base has a protected parameterless ctor)
    private Order(OrderId id, CustomerId customer) : base(id) => CustomerId = customer;
    public CustomerId CustomerId { get; private set; } = default!;

    public static Result<Order> Place(CustomerId customer, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
            return new OrderHasNoItems();

        var order = new Order(OrderId.New(), customer);
        order.AddDomainEvent(new OrderPlacedDomainEvent(order.Id, customer));
        return order;
    }
}
```

For custom-equality value objects use the `ValueObject` base class + `GetEqualityComponents()`; simple ones are plain records (ADR-004). Every error is a `sealed record … : Error(code, message)` with a ≥3-segment code; `Error` itself is abstract — see [error-model.md](error-model.md).

### Application

```csharp
public record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<OrderItemDto> Items) : ICommand<OrderId>;

public sealed class PlaceOrderHandler(IOrderRepository orders) : ICommandHandler<PlaceOrderCommand, OrderId>
{
    public async Task<AppResult<OrderId>> HandleAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        var order = Order.Place(new CustomerId(command.CustomerId), items);
        if (order.IsFailure) return order.Errors.Enrich();   // all errors; "nothing to add" said explicitly
        orders.Add(order.Value);                             // sync — change tracker; commit is UoW/TransactionBehavior's job
        return order.Value.Id;
    }
}
```

Void commands: `record CancelOrderCommand(Guid OrderId) : ICommand;` + `ICommandHandler<CancelOrderCommand>` with `Task<AppResult<Unit>> HandleAsync(...)` returning `Unit.Value`. Queries: `IQuery<TResult>` + `IQueryHandler<TQuery, TResult>`.

Repository interfaces (`IOrderRepository : IRepository<Order, OrderId>`) belong to the **Application** layer. The exact contract (do not wrap in `Result<>`):

```csharp
public interface IRepository<TAgg, TId> where TAgg : AggregateRoot<TId> where TId : notnull
{
    Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default);  // null when not found
    void Add(TAgg aggregate);      // sync — actual write happens at UoW commit
    void Delete(TAgg aggregate);
}
```

Validators delegate to the domain and are keyed by the concrete command type:

```csharp
public class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Email).MustBeValueObject(Email.Create);
        RuleFor(x => x.Items).ListMustContainNumberOfItems(min: 1, max: 50);
        RuleFor(x => x.CustomerId).NotEmpty();                       // plain rule → validation.rule.failed + details.rule
    }
}
```

### Infrastructure

```csharp
public class OrderDbContext(DbContextOptions<OrderDbContext> options) : KotoDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

`KotoDbContext` auto-applies strongly-typed-id converters and clears domain events after save. Two ways to register:

- **With Wolverine** (events leave the service): `AddKotoEFCore<OrderDbContext>(o => o.UseNpgsql(conn))` registers the context, `IRepository<,>`, `IUnitOfWork` (`EfCoreUnitOfWork<TContext>`, TryAdd — a hand-written one wins) and the outbox; configure `builder.Host.UseWolverine(...)` as the EFCore README shows.
- **Without Wolverine**: `AddDbContext<OrderDbContext>(...)`, hand-written repositories (`Repository<TAgg,TId>` base or your own) and your own `IUnitOfWork` over the context. Do not use `EfCoreUnitOfWork<TContext>` here when aggregates raise domain events: its `CommitAsync` throws instead of dropping events when no outbox exists. `IUnitOfWork` (Koto.Application) has **three members plus a defaulted property** — implement all of them, `TransactionBehavior` calls `BeginTransactionAsync` before the handler, `CommitAsync` on a successful `AppResult`, `RollbackAsync` otherwise:

  ```csharp
  using Microsoft.EntityFrameworkCore.Storage;   // IDbContextTransaction

  public sealed class OrderUnitOfWork(OrderDbContext db) : IUnitOfWork
  {
      private IDbContextTransaction? _tx;
      public bool HasActiveTransaction => _tx is not null;

      public async Task BeginTransactionAsync(CancellationToken ct = default) =>
          _tx ??= await db.Database.BeginTransactionAsync(ct);

      public async Task CommitAsync(CancellationToken ct = default)
      {
          var events = db.ChangeTracker.Entries<IHasDomainEvents>().SelectMany(e => e.Entity.DomainEvents).ToList();
          // dispatch `events` in-process here if anything listens; KotoDbContext clears them after the save
          await db.SaveChangesAsync(ct);
          if (_tx is not null) { await _tx.CommitAsync(ct); await _tx.DisposeAsync(); _tx = null; }
      }

      public async Task RollbackAsync(CancellationToken ct = default)
      {
          if (_tx is not null) { await _tx.RollbackAsync(ct); await _tx.DisposeAsync(); _tx = null; }
      }
  }
  ```

### Api

```csharp
public sealed class PlaceOrderEndpoint : MappedCommandEndpoint<PlaceOrderRequest, PlaceOrderCommand, OrderId>
{
    public override void Configure() => Post("/api/orders");
    protected override PlaceOrderCommand ToCommand(PlaceOrderRequest r) =>
        new(User.GetUserId(), r.Items);   // server-derived fields from claims, not the body
}
```

Plain `CommandEndpoint<TCommand[, TResult]>` / `QueryEndpoint<TQuery, TResult>` when the request *is* the command; route values bind into the command's properties by name (`Post("/api/meetups/{meetupId}/join")` fills `MeetupId`). Endpoints implement `Configure()` only — `HandleAsync` is sealed and dispatches through `ICqrsDispatcher`. Every response is the envelope `{ success, result, errors[], correlationId, timestamp }`: success → 200 (void commands: `result: null`), failure → status from `KotoApiOptions` (unmapped business codes **422**; customize via `AddKotoApi(o => o.Map(...))`).

### Program.cs

```csharp
builder.Services.AddKotoApi();                       // Envelope responses, status classifier, correlation, exception handler
builder.Services.AddFastEndpoints();
builder.Services.AddKotoApplication(
    o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(),   // opt-in; order = execution order
    typeof(PlaceOrderHandler).Assembly);
builder.Services.AddKotoValidation(typeof(PlaceOrderValidator).Assembly);           // scans IValidator<T> implementations
builder.Services.AddDbContext<OrderDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, OrderUnitOfWork>();                          // or AddKotoEFCore<OrderDbContext> with Wolverine

var app = builder.Build();
app.UseKotoApi();                                    // correlation middleware + global exception handler
app.UseKotoFastEndpoints();                          // instead of UseFastEndpoints(): binding errors in the envelope too
```

`AddKotoApplication` validates at startup that every `ICommand`/`IQuery` in the scanned assemblies has exactly one handler (`o.ValidateHandlers = false` to opt out).

### Tests

```csharp
public class ErrorCatalogTests
{
    [Fact]
    public void Catalog_is_valid() =>
        ErrorCatalog.ShouldBeValid(typeof(Order).Assembly, typeof(PlaceOrderHandler).Assembly);
}

[Fact]
public void Place_without_items_fails() =>
    Order.Place(customer, []).Should().BeFailureWith("orders.order.no-items");
```

Namespaces: `Koto.Testing.Assertions` for `Should()`, `Koto.Testing.Catalog` for `ErrorCatalog` (see the Namespaces table above).

## Gotchas

- **`AddKotoEFCore<TContext>` requires Wolverine** (it wires the outbox). Without Wolverine use plain `AddDbContext` + hand-written repositories and `IUnitOfWork` — everything else works.
- Behaviors are registered as open generics but resolved **per concrete command type**; validators as `AbstractValidator<ConcreteCommand>`.
- Auth: for `User.GetUserId()` to work with JWT, set `options.MapInboundClaims = true` in `AddJwtBearer` (maps `nameid` → `ClaimTypes.NameIdentifier`).
- FastEndpoints endpoints are secure by default — call `AllowAnonymous()` explicitly on public endpoints.
- `dotnet ef database update`: never use `--no-build` — a stale model triggers a false `PendingModelChangesWarning`.
- Wolverine discovers handlers by NAME convention: classes ending in `Handler`/`Consumer`. A class named `FooTranslator` with a valid `HandleAsync` is silently ignored — name in-process event translators `*Handler`.
- Kafka consumer group: `UseKotoKafka` **requires an explicit consumer group** (0.4.0 breaking — there is no default). Contracts are schema-transparent (raw JSON by convention, no envelope).
- Consumer retries: `UseKotoKafka` installs a default failure policy — inline retries 200ms/1s/3s, then scheduled 10s/30s/60s, then dead letter. Multi-topic races («prerequisite event not consumed yet») are routine: **throw** from the consumer to trigger a retry; a silent `return` loses data forever. Service-specific `opts.Policies.OnException<T>()` policies override the default. For dedup across restarts add `Koto.Messaging.Wolverine.Postgres` (`AddPostgresProcessedMessageStore`).
- Service-to-service auth: `X-Service-Key` header + `ServiceKey` scheme; `AddServiceHttpClient` no longer needs a hand-written `ICorrelationIdAccessor`.
- Pagination: return `PagedList<T>` via `ToPagedListAsync` — don't hand-roll skip/take. Fold `Result<T>` collections with `Result.Sequence`/`Traverse`; `Result` carries multiple errors, combine with `Result.Combine`, propagate with `return x.Errors;` (never `FirstError`).
- Handler results: handlers return `AppResult<T>`; a domain `Result<T>` leaves through `ForField(...)`, `Enrich()` or `Enrich(map)` — the compiler rejects a bare `Result<T>`/`Error`. `EnrichmentGuard` (Development) throws when a fuller typed error exists but was not used.
- Http ACL: derive from `ServiceHttpClient`, register with `AddServiceHttpClient<TInterface, TImpl>(...)`; Koto envelopes are unwrapped into `Result<T>` with `RemoteError`s; map non-envelope downstream errors in `MapErrorResponse` (defaults in `HttpErrors`).
- Domain events: `EfCoreUnitOfWork.CommitAsync` throws if events were raised and no outbox is registered — wire Wolverine or your own `IUnitOfWork`.
- Testing: `Result`/`AppResult` assertions come from `Koto.Testing` with **AwesomeAssertions** (`.Should().BeSuccess()`, `.BeFailureWith("code")`, `.HaveErrorForField("email")`); never add FluentAssertions v8+.
