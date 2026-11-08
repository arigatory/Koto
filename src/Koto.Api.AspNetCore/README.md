# Koto.Api.AspNetCore

ASP.NET Core integration for Koto results: map a handler's `AppResult<T>` to the Koto **Envelope**
response from **Minimal APIs**, **MVC controllers** or any other ASP.NET Core host.
`Koto.Api.FastEndpoints` builds on this package.

## What you get

- `ToHttpResult()` / `ToHttpResultAsync()` — `AppResult<T>` → `IResult` (Minimal API)
- `ToActionResult()` / `ToActionResultAsync()` — `AppResult<T>` → `ActionResult<Envelope<T>>` (MVC)
- `KotoApiOptions` — the coarse error → HTTP status classifier and the guard mode
- `EnrichmentGuard` — fails fast (Development) when a handler forgot a fuller typed error
- `ServerError` — the error behind every 500 (`general.server.internal-error`)

Only application results are accepted. A domain `Result<T>` has no `ToHttpResult` — it must leave
the handler through `ForField` / `Enrich` first (see `Koto.Application`).

## The response

```json
HTTP 200
{ "success": true,  "result": { "id": "…" }, "errors": [], "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:03Z" }

HTTP 422
{ "success": false, "result": null,
  "errors": [ { "code": "identity.otp.too-many-attempts", "message": "Too many attempts.",
                "field": null, "details": { "retryAfterSeconds": 42 } } ],
  "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:09Z" }
```

Void commands respond `200` with `"result": null`. Failures are sent as `Envelope<object?>` — the same
keys with `result: null` — so `Envelope<T>` is the declared schema for the endpoint (`.Produces<Envelope<T>>()`
in Minimal API, automatic in `Koto.Api.FastEndpoints`) and OpenAPI documents real properties.
Non-JSON responses (files, streams) stay outside the envelope. `timestamp` comes from the registered `TimeProvider` (`TimeProvider.System` unless you
register another one before `AddKotoAspNetCore`).

## Quick start

```csharp
builder.Services.AddKotoAspNetCore(); // optional: o => o.Map("payments.gateway.failed", 502)
```

Minimal API:

```csharp
app.MapPost("/orders", (CreateOrderCommand cmd, ICqrsDispatcher dispatcher, HttpContext ctx, CancellationToken ct)
    => dispatcher.SendAsync(cmd, ct).ToHttpResultAsync(ctx))
   .Produces<Envelope<OrderId>>();
```

MVC controller:

```csharp
[HttpPost]
public Task<ActionResult<Envelope<OrderId>>> Create(CreateOrderCommand cmd, CancellationToken ct)
    => _dispatcher.SendAsync(cmd, ct).ToActionResultAsync(this);
```

## Status codes — a class for infrastructure, not a second error code

Clients react to `errors[].code`; the status exists for the machinery that never reads the body
(401 refresh flows, retries on 5xx, alerting, caches). So the mapping is coarse:

| Rule (in order) | Status |
|---|---|
| exact code from `Map(code, status)` | as configured |
| custom rules from `Map(error => status?)` | as configured |
| `*.not-found` | 404 |
| `*.already-*`, `*.conflict` | 409 |
| `*.unauthorized` | 401 |
| `*.forbidden` | 403 |
| `general.*`, `validation.*`, error bound to a field | 400 |
| everything else | **422** (`FallbackStatusCode`) |

A failed result is a rule violation the client can act on — never 500. For several errors the
**first** one decides, and `Result.Combine` keeps the order you wrote. Customize:

```csharp
builder.Services.AddKotoAspNetCore(o => o
    .Map("payments.gateway.failed", StatusCodes.Status502BadGateway)
    .Map(e => e.Code.EndsWith(".rate-limited") ? StatusCodes.Status429TooManyRequests : null));
```

Rules receive the `EnrichedError` wrapper; match on the original type through `Inner`:
`o.Map(e => e is EnrichedError { Inner: QuotaExceeded } ? 429 : null)`.

## EnrichmentGuard — the second level of the error contract

The compiler guarantees the data of the error record a handler constructs. The guard checks, at the
API boundary, that the handler did not forget the **fuller** type: when the scanned assemblies
(`AddKotoApplication`) contain a record deriving from `EnrichedError` whose constructor takes an
error of type `X` — `MeetupFullWithAlternative(MeetupFull Cause, Guid AlternativeMeetupId)` — a bare
`MeetupFull` reaching the response is a violation.

| Environment | Default |
|---|---|
| Development | throw `InvalidOperationException` |
| anything else, including hosts without `IHostEnvironment` | log a warning, send the response as is |

Override with `o.EnrichmentGuard = EnrichmentGuardMode.Off / Warn / Throw`.

## Upgrading from 0.4

0.5 replaces Problem Details with the `Envelope<T>` (void commands answer 200, not 204), renames `KotoHttpErrorOptions` to `KotoApiOptions` (`MapSuffix` / `MapPrefix` → `Map(rule)`), accepts only `AppResult<T>` in `ToHttpResult` / `ToActionResult`, and deletes `KotoProblemDetails`. The step-by-step guide for the whole suite, with before/after code, is
[docs/migrations/0.4-to-0.5.md](https://github.com/arigatory/Koto/blob/main/docs/migrations/0.4-to-0.5.md).
