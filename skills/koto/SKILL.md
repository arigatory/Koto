---
name: koto
description: |
  Use when building or extending .NET services with the Koto.* NuGet packages
  (arigatory on nuget.org): scaffolding a new Koto microservice, adding an
  aggregate / value object / command / query / endpoint to a Koto service,
  defining or returning errors (Error catalog, Result, AppResult, EnrichedError,
  Envelope), reading Koto API responses in a client, migrating a service from
  Koto 0.4 to 0.5, choosing Koto packages, embedding the koto-agent React chat
  widget, releasing Koto itself, or when the user mentions "Koto" or DDD/CQRS in
  a project that references Koto.Domain or Koto.Application.
metadata:
  skill-version: "0.5.0"
  koto-version: "0.5.0-preview.1"
---

# Koto — DDD/CQRS building blocks for .NET

Koto is a suite of 14 NuGet packages (MIT, `net10.0`) by arigatory: DDD primitives, CQRS, Result-based error flow, EF Core/Marten persistence, Wolverine/Kafka messaging, FastEndpoints + ASP.NET Core API layers.

**Version line: 0.5.0-preview.1.** Install with `--prerelease` (`dotnet add package Koto.Domain --prerelease`) and keep every Koto package on the same version. 0.5 replaced the error model of the stable 0.4.0 line: abstract `Error` catalog, `AppResult<T>` for handlers, one `Envelope<T>` response for every transport (ADR-026). This skill describes **0.5** and marks where 0.4 differed. Pre-1.0: minor bumps carry breaking changes — read the guide in `docs/migrations/` of the Koto repo before upgrading (`0.4-to-0.5.md` for this line).

Sources of truth, in priority order:
1. Per-package READMEs — the text of each nuget.org package page; in the repo at `src/<Package>/README.md` (github.com/arigatory/Koto; a local checkout works the same).
2. ADRs (`adr/`) and `docs/principles/` in the Koto repo — the *why*. ADR-026 + `docs/packages/16-error-model-envelope.md` define the 0.5 error model.
3. This skill — conventions and the shape of the wiring. **For exact signatures read the package README; never guess from memory.** Namespaces are not in the READMEs — use the table in [references/dotnet-service.md](references/dotnet-service.md).

## Decision tree

| Task | Read |
|---|---|
| New .NET service from scratch | [references/dotnet-service.md](references/dotnet-service.md) |
| Add an aggregate / command / query / endpoint to an existing Koto service | [references/dotnet-service.md](references/dotnet-service.md) (Conventions) |
| Define an error, fail from a handler, map it to HTTP, read errors in a client | [references/error-model.md](references/error-model.md) |
| Upgrade a service from Koto 0.4 to 0.5 | [references/error-model.md](references/error-model.md) (Migration) |
| Call another service over HTTP (ACL) | [references/dotnet-service.md](references/dotnet-service.md) (Http section) |
| Embed the koto-agent AI chat widget in a React app | [references/widget.md](references/widget.md) |
| Change Koto itself: spec → plan → ADR → tests → README | [references/workflow.md](references/workflow.md) |
| Release Koto packages to nuget.org (maintainers) | [references/publishing.md](references/publishing.md) |

## Non-negotiable conventions

These are the rules agents most often get wrong when guessing. Follow them exactly.

1. **Always-valid domain.** Value objects and aggregates have private constructors + a static factory returning `Result<T>` (`Email.Create(string)`). Invalid state must be unconstructable.
2. **One rail, no exceptions for expected failures.** Domain returns `Result<T>` (errors: `Error`), handlers return `AppResult<T>` (errors: `EnrichedError`), the API sends `Envelope<T>`. Into `Result<T>` convert implicitly: a value, an error record, `other.Errors`. **There is no implicit `Result<T>` → `AppResult<T>`**: a domain failure leaves a handler through one of three verbs — `return x.Errors.ForField(nameof(cmd.Email));` (bound to a request field), `return x.Errors.Enrich();` (nothing to add — said explicitly), `return x.Errors.Enrich(e => e is MeetupFull f ? new MeetupFullWithAlternative(f, alt.Id) : e.Enrich());` (a fuller typed error). Success is implicit: `return dto;`. **Never propagate `FirstError`** — it drops the other errors. (0.4: handlers returned `Result<T>`, the property was `Error`.)
3. **Handlers:** `ICommandHandler<TCommand>` / `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` — `TResult` is the *bare* type, **no `Result<>`/`AppResult<>` in the type argument**. The method is `Task<AppResult<TResult>> HandleAsync(TCommand command, CancellationToken ct = default)`; void commands return `Task<AppResult<Unit>>` and `return Unit.Value;` — **`HandleAsync`, never `Handle`**. `AddKotoApplication` fails at startup when a scanned command/query has no handler (or several).
4. **Dispatcher is `ICqrsDispatcher`** (`SendAsync` / `QueryAsync`, both return `AppResult`). No `ISender`, no MediatR (MediatR v13+ is commercial — a license trap).
5. **Strongly-typed IDs** are records deriving from the base record:
   `public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value) { public static OrderId New() => new(Guid.NewGuid()); }`
   — not structs, not marker interfaces.
6. **Errors are types.** `Error` is **abstract** — `new Error("…", "…")` does not compile. Every error is a `sealed record` in a catalog, defined in the layer that detects it: `public sealed record OtpExpired(DateTimeOffset ExpiredAt) : Error("identity.otp.expired", "The code has expired.");` — record properties are the guaranteed data and reach clients as `details`. Codes are `context.entity.problem`, **at least three kebab-case segments**, validated on construction. Shared records: `new Errors.General.ValueIsRequired()`, `InvalidLength(min, max)`, `NotFound("order", id)`, `CollectionIsTooSmall(min, actual)` / `CollectionIsTooLarge(max, actual)`. `Field` and the optional `Details` dictionary live on `EnrichedError` (Koto.Application), never on a domain error. Data only the application knows goes into an application-level record (`record EmailTaken(string Email, Guid ExistingUserId) : Error("identity.email.taken", "…")`) or a typed enrichment (`record MeetupFullWithAlternative(MeetupFull Cause, Guid AlternativeMeetupId) : EnrichedError(Cause)`). Add `ErrorCatalog.ShouldBeValid(asm…)` (Koto.Testing) as a unit test. No `ErrorType` enum, no `Error.Validation()/NotFound()` factories.
7. **`IRepository<TAgg, TId>` lives in `Koto.Application`**, not Domain (ADR-005). `Add`/`Delete` are sync (change tracker); `GetByIdAsync` returns `Task<TAgg?>` (null when missing, **not** `Result<>`); commit belongs to `IUnitOfWork` / `TransactionBehavior`. Domain events: `AddDomainEvent(...)` on `AggregateRoot<TId>` (not `Raise`).
8. **Pipeline behaviors are opt-in**, order of registration = order of execution: `AddKotoApplication(o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(), asm)` + `AddKotoValidation(asm)` (scans validators). `AddValidationBehavior()` is an extension from Koto.Validation — new in 0.5 (in 0.4 `AddKotoValidation` appended the behavior itself, after the others). Nothing commits automatically unless `TransactionBehavior` is registered. Validation failures arrive as `EnrichedError`s bound to their field; rules not backed by a domain factory become `validation.rule.failed` with `details.rule`. Only `MustBeValueObject` and `ListMustContainNumberOfItems` exist (`MustBeEntity` is gone).
9. **API wiring:** `AddKotoApi()` + `AddFastEndpoints()`, then `app.UseKotoApi()` + `app.UseKotoFastEndpoints()` (0.4: `UseFastEndpoints()`). Endpoints derive from `CommandEndpoint<TCommand[, TResult]>` / `QueryEndpoint<TQuery, TResult>` / `MappedCommandEndpoint` / `MappedQueryEndpoint` (namespace `Koto.Api.FastEndpoints.Endpoints`) and implement only `Configure()` (plus `ToCommand`/`ToQuery` for mapped ones) — `HandleAsync` is sealed. **Every response is the envelope** `{ success, result, errors: [{ code, message, field, details }], correlationId, timestamp }`: 200 on success (void commands too, `result: null`), failures get a coarse status from `KotoApiOptions` (`*.not-found` 404, `*.already-*`/`*.conflict` 409, `*.unauthorized` 401, `*.forbidden` 403, `general.*`/`validation.*`/field-bound 400, otherwise 422; the first error decides). Minimal API / MVC without FastEndpoints: `AddKotoAspNetCore()` and `dispatcher.SendAsync(cmd, ct).ToHttpResultAsync(ctx)` / `.ToActionResultAsync(this)` — they accept `AppResult<T>` only. No ProblemDetails, no `errorCode`/`title`/`detail`/`status` in the body. There is no `KotoEndpoint`, no `AddKotoFastEndpoints`.
10. **`AddKotoEFCore<TContext>` wires the Wolverine outbox and requires Wolverine.** Without Wolverine: plain `AddDbContext<TContext>` (context derives from `KotoDbContext`) + hand-written repositories and your own `IUnitOfWork` — the interface has `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync` (+ defaulted `HasActiveTransaction`); implement all three, `TransactionBehavior` drives them.
11. **Domain events need an outbox.** `EfCoreUnitOfWork.CommitAsync` throws when tracked aggregates raised events and no Wolverine outbox is registered — configure Wolverine or your own `IUnitOfWork`; events are never dropped silently.
12. **Events:** `IDomainEvent` is internal and never leaves the service; `IIntegrationEvent`/`IIntegrationCommand` are versioned public contracts (prefix `Integration` = crosses a service boundary).
13. **License traps:** never add MediatR v13+, MassTransit v9+, FluentAssertions v8+ (commercial). Use Wolverine, Marten, AwesomeAssertions. FluentValidation v12 is fine (Apache 2.0, pin `[12.0.0,13.0.0)`).
14. **Kafka messaging (`Koto.Messaging.Wolverine`):** `UseKotoKafka` **requires an explicit consumer group** (no default since 0.4.0). The bus is *schema-transparent* — integration contracts serialize as raw JSON by convention (no envelope). A default consumer retry policy is installed; **throw** from a consumer to retry (a silent `return` on an unmet prerequisite loses data). For dedup that survives restarts add `Koto.Messaging.Wolverine.Postgres` → `AddPostgresProcessedMessageStore(connString)`.
15. **Calling another service:** `Koto.Infrastructure.Http` — derive from `ServiceHttpClient`, register with `AddServiceHttpClient<TInterface, TImpl>(...)`; `ReadResultAsync<T>` unwraps a Koto envelope into `Result<T>` whose failures are `RemoteError`s (code/message/field/details) — the handler enriches them like any other error. Machine-to-machine auth: `X-Service-Key` header + `ServiceKey` scheme on both ends.
16. **Pagination & Result combinators:** page queries with `PagedList<T>` + `ToPagedListAsync` (`Koto.Application` / EF Core), not hand-rolled skip/take DTOs. `Result<T>` is **multi-error** — collect with `Result.Combine`, fold collections with `Result.Sequence` / `Result.Traverse`, propagate with `return x.Errors;` (all of them). `AppResult<T>` is the handler's final answer: `Map`, `Match`, `MatchAsync` only — no `Bind`/`Tap`/`Ensure`; build chains on the domain `Result<T>`.
17. **Testing:** `Koto.Testing` — `AggregateTestFixture`, `FakeRepository`, AwesomeAssertions-based `result.Should().BeSuccess()` / `.BeFailureWith("code")` / `.HaveErrors(...)`, for `AppResult` also `.HaveErrorForField("email")`, and `ErrorCatalog.Scan/ShouldBeValid`. `Koto.Testing.Integration` — `HeaderTestAuthHandler` (`AddHeaderTestAuthentication` + `WithTestUser`) for auth in `WebApplicationFactory`, `Eventually.AssertAsync` for async assertions, `RawJsonKafkaProducer` for feeding raw-JSON contracts; no container deps of its own — compose with Testcontainers.
18. **Clients (TypeScript, mobile, other services) branch on `success`,** then react per `errors[].code` and `errors[].details`, and bind `errors[].message` to the input named by `errors[].field` (camelCase, matches the JSON they sent). The HTTP status is a coarse class, never the primary signal.
