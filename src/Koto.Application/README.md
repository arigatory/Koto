# Koto.Application

CQRS dispatcher, pipeline behaviors, the application-layer error model (`EnrichedError`,
`AppResult<T>`, `Envelope<T>`) and cross-service integration abstractions. Part of the
[Koto](https://github.com/arigatory/Koto) suite.

## Install

```bash
dotnet add package Koto.Application
```

## What's included

| Type | Purpose |
|---|---|
| `ICommand` / `ICommand<TResult>` / `IQuery<TResult>` | Marker interfaces for write and read operations |
| `ICommandHandler<T>` / `ICommandHandler<T, TResult>` / `IQueryHandler<TQuery, TResult>` | Handler contracts — all return `Task<AppResult<…>>` |
| `ICqrsDispatcher` | Dispatches commands and queries through the pipeline |
| `IPipelineBehavior<TRequest, TResponse>` | Middleware for the CQRS pipeline |
| `KotoApplicationOptions` | Opt-in behaviors (`AddLoggingBehavior()`, `AddTransactionBehavior()`, `AddBehavior(type)`), `ValidateHandlers` |
| `LoggingBehavior<,>` / `TransactionBehavior<,>` | Built-in behaviors |
| `IUnitOfWork` / `IRepository<TAgg, TId>` | Persistence ports implemented by infrastructure |
| `AppResult<T>` | The result type of handlers: a value or an `ErrorList<EnrichedError>` |
| `EnrichedError` | A domain `Error` plus request context: `Field` and optional `Details`; keeps the original as `Inner` |
| `ErrorEnrichment` | The explicit verbs `ForField`, `Enrich()`, `Enrich(map)`, `WithDetails` |
| `ErrorDetails` | Flattens an error's record properties into wire `details` |
| `Envelope<T>` / `EnvelopeError` | The one response shape for every transport |
| `IIntegrationEvent` / `IntegrationEvent` / `IIntegrationCommand` | Cross-service contracts |
| `IIntegrationEventPublisher` / `IIntegrationCommandDispatcher` | Implemented by `Koto.Messaging.Wolverine` |
| `IntegrationEventTopics` | `Topic` constant convention for integration contracts |
| `PagedList<T>` | Page of results with metadata |
| `KotoApplicationAssemblies` | The scanned assemblies (used by the API layer's error-catalog checks) |

## Register

```csharp
builder.Services.AddKotoApplication(
    o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(),  // order = execution order
    typeof(PlaceOrderHandler).Assembly);
builder.Services.AddKotoValidation(typeof(PlaceOrderHandler).Assembly);           // scans validators
```

`AddKotoApplication` fails at startup when a command or query in the scanned assemblies has no
handler, or more than one — a missing handler is found before the first request, not by it.
Set `o.ValidateHandlers = false` if handlers are registered by hand.

## Three layers, two result types

| Layer | Returns | Errors |
|---|---|---|
| Domain | `Result<T>` | `Error` records from the catalog |
| Application (handlers) | `AppResult<T>` | `EnrichedError` |
| API | `Envelope<T>` | `EnvelopeError` (flat: `code`, `message`, `field`, `details`) |

There is **no implicit conversion** from `Result<T>` to `AppResult<T>`. Leaving a handler, a failure
goes through one of three explicit verbs, and each says what was added:

```csharp
public sealed class VerifyOtpHandler(IOtpRepository otps, IMeetupRepository meetups, IUnitOfWork uow)
    : ICommandHandler<VerifyOtpCommand, TokenPairDto>
{
    public async Task<AppResult<TokenPairDto>> HandleAsync(VerifyOtpCommand command, CancellationToken ct = default)
    {
        var email = Email.Create(command.Email);
        if (email.IsFailure) return email.Errors.ForField(nameof(command.Email));   // 1. bound to a request field

        var challenge = await otps.GetLatestAsync(email.Value, ct);
        if (challenge is null) return new OtpInvalidCode(attemptsLeft: 0).Enrich();   // application-level error

        var verified = challenge.Verify(command.Code, DateTimeOffset.UtcNow);
        if (verified.IsFailure) return verified.Errors.Enrich();                      // 2. nothing to add — said explicitly

        var joined = meetup.Join(user.Id);
        if (joined.IsFailure)
            return joined.Errors.Enrich(e => e is MeetupFull full                      // 3. fuller typed error with data from the DB
                ? new MeetupFullWithAlternative(full, alternative.Id)
                : e.Enrich());

        await uow.CommitAsync(ct);
        return new TokenPairDto(...);                                                   // success: implicit
    }
}
```

Typed enrichment is a record deriving from `EnrichedError` that wraps the domain error and adds
guaranteed properties:

```csharp
public sealed record MeetupFullWithAlternative(MeetupFull Cause, Guid AlternativeMeetupId)
    : EnrichedError(Cause);
```

On the wire the chain is flattened: `details = { "capacity": 8, "alternativeMeetupId": "…" }`.
`EnrichedError.Details` (dictionary, via `WithDetails`) is for optional additions only — the
record properties are the contract.

Several domain results can be bound to fields first and combined later:

```csharp
var fields = Result.Combine(
    Email.Create(cmd.Email).ForField("email"),
    Phone.Create(cmd.Phone).ForField("phone"));
if (fields.IsFailure) return fields.Errors.Enrich();
```

## Envelope — one shape for every transport

```json
{ "success": true,  "result": { "id": "…" }, "errors": [], "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:03Z" }

{ "success": false, "result": null,
  "errors": [ { "code": "identity.otp.too-many-attempts", "message": "Too many attempts.",
                "field": null, "details": { "retryAfterSeconds": 42 } } ],
  "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:09Z" }
```

`Envelope<T>` lives here, not in the HTTP package, so a SignalR hub or a Kafka reply can return it too:

```csharp
public async Task<Envelope<ChatReplyDto>> Ask(AskCommand command)          // SignalR hub method
{
    var result = await dispatcher.SendAsync<ChatReplyDto>(command, Context.ConnectionAborted);
    return result.ToEnvelope(timeProvider.GetUtcNow());
}
```

On HTTP every failure is sent as `Envelope<object?>`, so `result` is `null` whatever the result type;
`ToEnvelope<T>` on a typed transport (a hub method returning `Envelope<Guid>`) yields `default(T)` on
failure, so prefer reference DTOs there. `field` is camelCased per path segment to match the JSON the
client sent; `success` is the discriminator clients branch on. HTTP status codes remain meaningful and
are chosen by the API layer (`Koto.Api.AspNetCore`).

## Pipeline behaviors

Behaviors are resolved against the **concrete** command/query type: an open-generic registration
like `ValidationBehavior<,>` closes over `PlaceOrderCommand` at dispatch time, so
`IValidator<PlaceOrderCommand>` is discovered. A behavior can also target a single request by
implementing `IPipelineBehavior<PlaceOrderCommand, AppResult<OrderId>>`.

`TransactionBehavior` commits on success and rolls back on a failed `AppResult`; commands marked
`INonTransactionalCommand` manage `IUnitOfWork` themselves.

## Pagination

`PagedList<T>` carries `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNextPage`,
`HasPreviousPage`. Return it from query handlers (`IQuery<PagedList<OrderDto>>`); materialize from
EF Core with `ToPagedListAsync` in `Koto.Infrastructure.EFCore`.
