# Koto.Api.FastEndpoints

FastEndpoints integration for Koto DDD building blocks. Builds on **Koto.Api.AspNetCore**
(the `Envelope<T>` response, `KotoApiOptions` status classifier, `EnrichmentGuard`).

## What's included

| Type | Purpose |
|---|---|
| `CommandEndpoint<TCommand>` | Dispatches a void command → `Envelope<object?>` with `result: null` |
| `CommandEndpoint<TCommand, TResult>` | Dispatches a result command → `Envelope<TResult>` |
| `QueryEndpoint<TQuery, TResult>` | Dispatches a query → `Envelope<TResult>` |
| `MappedCommandEndpoint<TRequest, TCommand[, TResult]>` | Maps an HTTP request DTO to a command via `ToCommand` — keeps server-derived fields out of the wire contract |
| `MappedQueryEndpoint<TRequest, TQuery, TResult>` | Maps an HTTP request DTO to a query via `ToQuery` |
| `UseKotoFastEndpoints()` / `MapKotoFastEndpoints()` | `UseFastEndpoints` with binding and validator failures rendered as the envelope |
| `ClaimsPrincipalExtensions.GetUserId()` | Reads the user id from the `NameIdentifier` claim (`TryGetUserId` for the no-throw path) |
| `CorrelationIdMiddleware` / `ICorrelationIdAccessor` | Reads/generates `X-Correlation-ID`, echoes it in the response and in every envelope |
| `GlobalExceptionHandler` | Unhandled exception → 500 envelope with `general.server.internal-error` |

Every response of a Koto endpoint is the envelope: `200` with `success: true` on success, the status
chosen by `KotoApiOptions` (404 / 409 / 400 / 422 …) with `success: false` and **all** errors on
failure. The base classes declare `Envelope<TResult>` as the FastEndpoints response type, so the
generated OpenAPI matches what is sent.

## Setup

```csharp
// Program.cs
builder.Services.AddKotoApi();          // Koto.Api.AspNetCore + correlation id + exception handler
// Or customize the error → status classifier:
builder.Services.AddKotoApi(o => o.Map("payments.gateway.failed", 502));
builder.Services.AddFastEndpoints();
// ... other services

var app = builder.Build();
app.UseKotoApi();                        // correlation id middleware + exception handler
app.UseKotoFastEndpoints();              // instead of UseFastEndpoints(): binding errors in the envelope too
app.Run();
```

## Implementing an endpoint

`HandleAsync` is sealed on all base endpoints — you only write `Configure()`:

```csharp
// Void command → 200 { success: true, result: null }
public class DeleteOrderEndpoint : CommandEndpoint<DeleteOrderCommand>
{
    public override void Configure()
    {
        Delete("/orders/{id}");
        AllowAnonymous();
    }
}

// Command with result → 200 { success: true, result: <OrderId> }
public class PlaceOrderEndpoint : CommandEndpoint<PlaceOrderCommand, OrderId>
{
    public override void Configure() => Post("/orders");
}

// Query → 200 / 404 (*.not-found) with the envelope
public class GetOrderEndpoint : QueryEndpoint<GetOrderQuery, OrderDto>
{
    public override void Configure()
    {
        Get("/orders/{id}");
        AllowAnonymous();
    }
}
```

## Server-derived fields (claims, route, tenant)

When a command/query carries fields that must come from the server (the caller's user id, tenant,
correlation id) and **must not** be bindable from the request body, use the mapped endpoints. The
request DTO omits those fields; `ToCommand`/`ToQuery` builds the command from the request **and** the
endpoint context (`User`, `Route<T>()`, headers):

```csharp
public sealed record SubmitJudgmentRequest(Guid SubmissionId, int GoeScore); // no JudgeId on the wire
public sealed record SubmitJudgmentCommand(Guid JudgeId, Guid SubmissionId, int GoeScore) : ICommand<Judgment>;

public sealed class SubmitJudgmentEndpoint
    : MappedCommandEndpoint<SubmitJudgmentRequest, SubmitJudgmentCommand, Judgment>
{
    public override void Configure() { Post("/api/v1/judgments"); Policies("IsJudge"); }

    protected override SubmitJudgmentCommand ToCommand(SubmitJudgmentRequest r) =>
        new(JudgeId: User.GetUserId(), r.SubmissionId, r.GoeScore); // JudgeId from claims, not the body
}
```

Prefer the plain `CommandEndpoint`/`QueryEndpoint` when the request **is** the command (no server-derived
fields). `HandleAsync` is sealed on the mapped variants — you only implement `Configure` and `ToCommand`/`ToQuery`.

## Binding and validator failures

FastEndpoints' own failures (JSON binding, `Validator<TRequest>` rules) are sent through the same
envelope with status 400: a failure carrying a Koto `Error` in `CustomState` (`MustBeValueObject`)
keeps its code, any other rule becomes `validation.rule.failed` with `details.rule`. The `field`
is camelCased per path segment (`items[0].name`), matching the JSON property names.

## Error code → HTTP status mapping

Status codes come from `KotoApiOptions` (Koto.Api.AspNetCore): exact code → custom rules →
`*.not-found` 404 → `*.already-*` / `*.conflict` 409 → `*.unauthorized` 401 → `*.forbidden` 403 →
`general.*` / `validation.*` / field-bound 400 → **422** fallback. Customize via
`AddKotoApi(o => o.Map("payments.gateway.failed", 502))`.

## Upgrading from 0.4

0.5 replaces `app.UseFastEndpoints()` with `app.UseKotoFastEndpoints()`, seals `HandleAsync` on every base endpoint (`SendCommandAsync` / `SendQueryAsync` are gone — endpoints implement `Configure()` only) and sends every response as the `Envelope<T>`. The step-by-step guide for the whole suite, with before/after code, is
[docs/migrations/0.4-to-0.5.md](https://github.com/arigatory/Koto/blob/main/docs/migrations/0.4-to-0.5.md).
