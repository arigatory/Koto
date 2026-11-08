# Errors, results and the envelope (Koto 0.5)

One failure travels as a value from the aggregate to the client. Three layers, three types, one shape on the wire.

| Layer | Returns | Error type | Package |
|---|---|---|---|
| Domain | `Result<T>` | `Error` — abstract, every error is a catalog record | Koto.Domain |
| Application (handlers, dispatcher) | `AppResult<T>` | `EnrichedError` — `Inner` + `Field` + `Details` | Koto.Application |
| API (HTTP, SignalR, Kafka reply) | `Envelope<T>` | `EnvelopeError` `{ code, message, field, details }` | Koto.Application (`Envelope`), Koto.Api.* (HTTP status) |

## 1. Define errors as records

```csharp
// Domain — the layer that detects the failure owns the record. Properties = guaranteed `details`.
public sealed record MeetupFull(int Capacity)
    : Error("events.meetup.full", "The meetup is full.");

public sealed record OtpTooManyAttempts(int RetryAfterSeconds)
    : Error("identity.otp.too-many-attempts", "Too many attempts.");

// Application — data only the application knows (from the DB, another service):
public sealed record EmailTaken(string Email, Guid ExistingUserId)
    : Error("identity.email.taken", "This email is already registered.");

// Typed enrichment — a fuller version of a domain error, built in the handler:
public sealed record MeetupFullWithAlternative(MeetupFull Cause, Guid AlternativeMeetupId)
    : EnrichedError(Cause);
```

- `Error` is abstract: `new Error("…", "…")` does not compile. Write `sealed record X(...) : Error(code, message)`.
- Code = `context.entity.problem`, **at least three kebab-case segments** (`^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*){2,}$`), checked in the constructor — a bad code throws when the record is created, so cover the catalog with the test below.
- Shared records in `Errors.General` (create with `new`): `ValueIsRequired()` → `general.value.is-required`, `InvalidLength(int Min, int Max)` → `general.value.invalid-length`, `NotFound(string Entity, object? Id = null)` → `general.entity.not-found`, `CollectionIsTooSmall(int Min, int Actual)` → `general.collection.too-small`, `CollectionIsTooLarge(int Max, int Actual)` → `general.collection.too-large`.
- `Message` is a human-readable default and is `init`-able: `new MeetupFull(8) { Message = "…" }`.
- No `Field` on a domain error — the domain does not know request fields. No `ErrorType` enum — the code carries the meaning.
- Catalog test (Koto.Testing), one per service — checks code format and uniqueness for every record in the assemblies (typed enrichments share their cause's code and are not counted as duplicates):

  ```csharp
  [Fact]
  public void Error_catalog_is_valid() =>
      ErrorCatalog.ShouldBeValid(typeof(Meetup).Assembly, typeof(JoinMeetupHandler).Assembly);
  ```

  `ErrorCatalog.Scan(...)` returns `(Type, Code, Message, DetailKeys)` per record — the input for generating client-side error maps.

## 2. Return failures from the domain

```csharp
public static Result<Email> Create(string value) =>
    string.IsNullOrWhiteSpace(value) ? new Errors.General.ValueIsRequired() :
    value.Length > 150              ? new Errors.General.InvalidLength(1, 150) :
                                      new Email(value);

public Result<Unit> Join(UserId user)
{
    if (_participants.Count >= Capacity) return new MeetupFull(Capacity);
    if (_participants.Contains(user))    return new MeetupAlreadyJoined(Id, user);
    _participants.Add(user);
    AddDomainEvent(new UserJoinedMeetup(Id, user));
    return Unit.Value;
}
```

- Implicit conversions into `Result<T>`: a `T`, any `Error` record, an `ErrorList<Error>` (`return other.Errors;`).
- Several factories: `Result.Combine(Email.Create(a), Name.Create(b))` collects **all** errors. Collections: `Result.Sequence(results)`, `Result.Traverse(items, Item.Create)`, `Result.TraverseAsync(items, CreateAsync, FailureMode.FailFast, ct)`.
- `result.Errors` is the whole list (`ErrorList<Error>`, an `IReadOnlyList<Error>`); `result.FirstError` is for display and status only — propagating it drops the rest.
- Void flows: `Result<Unit>`; `Result.Success()` / `Result.Failure(error)`; `return Unit.Value;`.

## 3. Leave the handler: `AppResult<T>`

```csharp
public sealed class JoinMeetupHandler(IMeetupRepository meetups) : ICommandHandler<JoinMeetupCommand>
{
    public async Task<AppResult<Unit>> HandleAsync(JoinMeetupCommand cmd, CancellationToken ct = default)
    {
        var meetup = await meetups.GetByIdAsync(new MeetupId(cmd.MeetupId), ct);
        if (meetup is null)
            return new Errors.General.NotFound("meetup", cmd.MeetupId).ForField(nameof(cmd.MeetupId)); // 1. bound to a field

        var joined = meetup.Join(new UserId(cmd.UserId));
        if (joined.IsFailure)
        {
            var alternative = await meetups.FindAlternativeAsync(meetup.Id, ct);
            return joined.Errors.Enrich(e => e is MeetupFull full && alternative is not null          // 3. fuller typed error
                ? new MeetupFullWithAlternative(full, alternative.Id.Value)
                : e.Enrich());                                                                       // 2. nothing to add
        }

        return Unit.Value;                                                                           // success: implicit
    }
}
```

The compiler rejects `return joined.Errors;` and `return new MeetupFull(8);` inside a handler — a domain failure passes through exactly one verb:

| Verb | Defined on | Returns | Meaning |
|---|---|---|---|
| `.ForField("email")` | `Error`; `ErrorList<Error>`; `Result<T>` | `EnrichedError`; `ErrorList<EnrichedError>`; `Result<T>` (for `Result.Combine` before leaving) | Bind to the request field the client sent — `nameof(cmd.Email)`; the API camelCases it |
| `.Enrich()` | `Error`; `ErrorList<Error>`; `Result<T>` | `EnrichedError`; `ErrorList<EnrichedError>`; **`AppResult<T>`** | Nothing to add — stated explicitly |
| `.Enrich(Func<Error, EnrichedError> map)` | `ErrorList<Error>` | `ErrorList<EnrichedError>` | Replace some errors with fuller typed records, `e.Enrich()` for the rest |
| `.WithDetails(("key", value), …)` | `Error` | `EnrichedError` | Optional extras in the `Details` dictionary; record properties stay the contract |

- Several field-bound results first, combine later: `var fields = Result.Combine(Email.Create(c.Email).ForField("email"), Phone.Create(c.Phone).ForField("phone")); if (fields.IsFailure) return fields.Errors.Enrich();`
- Application-level failure: `return new EmailTaken(email, existing.Id).Enrich();` or `.ForField("email")`.
- Whole domain result with nothing to add: `return order.Enrich();` converts `Result<T>` straight into `AppResult<T>` (success included).
- `AppResult<T>` members: `IsSuccess`/`IsFailure`, `Value`, `Errors` (`ErrorList<EnrichedError>`), `FirstError`, `Map`, `Match`, `MatchAsync`; implicit from `T`, `EnrichedError`, `ErrorList<EnrichedError>`; static `AppResult.Success()` / `AppResult.Failure(...)` for `Unit`. No `Bind`/`Tap`/`Ensure` — chain on the domain `Result<T>`; `AppResult` is the final answer.
- `EnrichedError(Error inner)`: `Inner` (the original record; wrapping an `EnrichedError` collapses to one level), `Field`, `Details`; `Code` and `Message` come from the inner error. Typed enrichments derive from it: `record X(MeetupFull Cause, Guid AlternativeMeetupId) : EnrichedError(Cause)` — give such records no other `Error`-typed parameters.
- `EnrichmentGuard` (Koto.Api.AspNetCore; Development → throws, elsewhere → warns) catches a bare `MeetupFull` reaching the response once a record deriving from `EnrichedError` with a `MeetupFull` constructor parameter exists in the scanned assemblies. Having created `MeetupFullWithAlternative`, route every `MeetupFull` through it (or map it explicitly).

## 4. Validation (Koto.Validation)

```csharp
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Email).MustBeValueObject(Email.Create);      // domain factory — one source of truth
        RuleFor(x => x.Items).ListMustContainNumberOfItems(1, 50);
        RuleFor(x => x.CustomerId).NotEmpty();                       // plain FluentValidation rule
    }
}
```

`ValidationBehavior` short-circuits the handler and returns one `EnrichedError` per failure, bound to its field: a `MustBeValueObject` failure keeps the domain record (`general.value.is-required`, `identity.email.invalid`, …) with its properties as `details`; any other rule becomes `validation.rule.failed` with `details.rule` = the FluentValidation error code (`NotEmptyValidator`, or your `WithErrorCode` value). Validators run sequentially, so async rules may share scoped services. Only `MustBeValueObject` and `ListMustContainNumberOfItems` exist — 0.4's `MustBeEntity`, `NotEmptyWithKotoError`, `LengthWithKotoError` are gone.

## 5. On the wire: `Envelope<T>`

```json
HTTP 200  { "success": true,  "result": { "id": "7c9e…" }, "errors": [],
            "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:03Z" }

HTTP 422  { "success": false, "result": null,
            "errors": [ { "code": "events.meetup.full", "message": "The meetup is full.",
                          "field": null, "details": { "capacity": 8, "alternativeMeetupId": "…" } } ],
            "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:09Z" }
```

- Success is wrapped too; void commands answer 200 with `result: null` (0.4: 204). Failures are sent as `Envelope<object?>` — `result` is `null` whatever the result type.
- `details` = the record properties of the whole chain (inner record → typed wrapper → `WithDetails`), camelCase keys. `field` is camelCased per path segment (`Items[0].Email` → `items[0].email`).
- FastEndpoints: the base endpoints declare `Envelope<TResult>` as the response type; `UseKotoFastEndpoints()` (or `MapKotoFastEndpoints()` on a route builder) renders binding and validator failures in the same envelope.
- Minimal API / MVC: `dispatcher.SendAsync(cmd, ct).ToHttpResultAsync(httpContext)` / `.ToActionResultAsync(this)` (sync: `ToHttpResult` / `ToActionResult`). Only `AppResult<T>` is accepted — a domain `Result<T>` has no `ToHttpResult`.
- Other transports: `appResult.ToEnvelope(timeProvider.GetUtcNow(), correlationId)` — a SignalR hub method returns `Envelope<ChatReplyDto>`; prefer reference DTOs there (a failed `Envelope<Guid>` carries `default`).

### HTTP status — a coarse class, chosen by `KotoApiOptions`

| Rule (first match; with several errors the first error decides) | Status |
|---|---|
| `Map("payments.gateway.failed", 502)` — exact code | as configured |
| `Map(e => e.Code.EndsWith(".rate-limited") ? 429 : null)` — custom rule | as configured |
| code ends with `.not-found` | 404 |
| code contains `.already-` or ends with `.conflict` | 409 |
| code ends with `.unauthorized` / `.forbidden` | 401 / 403 |
| code starts with `general.` or `validation.`, or the error is bound to a `field` | 400 |
| anything else — a business rule said no | 422 (`FallbackStatusCode`) |
| unhandled exception (`UseKotoApi` exception handler) | 500, envelope with `general.server.internal-error` |

Configure with `AddKotoApi(o => o.Map(...))` (FastEndpoints) or `AddKotoAspNetCore(o => o.Map(...))`; `o.EnrichmentGuard = EnrichmentGuardMode.Off / Warn / Throw` overrides the environment default.

## 6. Clients

- Branch on `success`. On failure walk `errors[]`: switch on `code` for behaviour (disable a button, restart a timer, offer `details.alternativeMeetupId`), bind `message` to the input named by `field`, show the rest globally.
- The HTTP status is transport information; 4xx/5xx bodies are the same envelope.
- Another Koto service: `ServiceHttpClient.ReadResultAsync<T>(response, ct)` → `Result<T>`; failures are `RemoteError` records (`code`, `message`, `field`, `details`) — enrich them in the handler like any other error. Non-Koto responses map through `MapErrorResponse` (defaults in `HttpErrors`: `general.resource.not-found`, `general.resource.conflict`, `general.request.invalid`, `general.server.unexpected`, `general.server.http-error`).

## 7. Tests

```csharp
Email.Create("").Should().BeFailureWith("general.value.is-required");                                // Result<T>
meetup.Join(user).Should().BeSuccess();
appResult.Should().BeFailureWith("general.entity.not-found").And.HaveErrorForField("meetupId");     // AppResult<T>
```

`Koto.Testing` builds on AwesomeAssertions (never FluentAssertions v8+). Also `.BeFailure()`, `.HaveErrors("a", "b")`, `.HaveValue(x)`. `HaveErrorForField` is case-insensitive — assert with the wire name (`"meetupId"`) even though the handler bound `nameof(cmd.MeetupId)`.

## Migration 0.4 → 0.5 (consumer checklist)

Full guide with before/after code for every change: `docs/migrations/0.4-to-0.5.md` in the Koto repo
(https://github.com/arigatory/Koto/blob/main/docs/migrations/0.4-to-0.5.md). The mechanical steps, in order:

1. `new Error("code", "msg")` → one `sealed record` per error; `Errors.General.X(...)` → `new Errors.General.X(...)`; codes to ≥3 segments (`general.value.invalid-length`, `general.entity.not-found`, `general.collection.too-small` / `too-large`). Add the `ErrorCatalog.ShouldBeValid` test.
2. `result.Error` → `result.FirstError` (display only); `return x.Error;` → `return x.Errors;`. `TapError` → `TapErrors`; `Match` receives the whole error list. `IResultFactory`, `IDomainService` are gone.
3. Handlers: `Task<Result<T>>` → `Task<AppResult<T>>`; every `return domainResult.Errors;` → `.ForField(...)` / `.Enrich()` / `.Enrich(map)`; `Error.Field` → `EnrichedError.Field`; `ICqrsDispatcher` returns `AppResult`.
4. `AddKotoApplication(o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(), asm)` + `AddKotoValidation(asm)`; replace `MustBeEntity` / `NotEmptyWithKotoError` / `LengthWithKotoError` with `MustBeValueObject` or plain rules.
5. `app.UseFastEndpoints()` → `app.UseKotoFastEndpoints()`; drop `AddProblemDetails`; `KotoHttpErrorOptions` → `KotoApiOptions` (`MapSuffix`/`MapPrefix` → `Map(rule)`); delete `KotoProblemDetails` usages; `SendCommandAsync`/`SendQueryAsync` are gone — endpoints implement `Configure()` only.
6. Clients: read `success` and `errors[].code/message/field/details`; `errorCode`, `errorCodes`, `title`, `detail`, `status` no longer exist in the body; void commands return 200 + `result: null` instead of 204.
7. `ServiceHttpClient` callers: `ReadResultAsync<T>` still returns `Result<T>`; remote failures are `RemoteError` — enrich before returning from the handler.
8. EF Core: an outbox (Wolverine) or a custom `IUnitOfWork` is required where aggregates raise events — `EfCoreUnitOfWork.CommitAsync` now throws instead of dropping them. `Specification*` types are removed.
9. Marten consumers: Marten 8 → 9 — re-check projections.
