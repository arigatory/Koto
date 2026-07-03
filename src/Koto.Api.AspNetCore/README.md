# Koto.Api.AspNetCore

Transport-agnostic ASP.NET Core integration for Koto results: map `Result<T>` to HTTP
responses from **Minimal APIs**, **MVC controllers**, or any other ASP.NET Core host.
`Koto.Api.FastEndpoints` builds on this package.

## What you get

- `ToHttpResult()` / `ToHttpResultAsync()` — `Result<T>` → `IResult` (Minimal API)
- `ToActionResult()` / `ToActionResultAsync()` — `Result<T>` → `ActionResult<T>` (MVC)
- `KotoProblemDetails` — RFC 7807 Problem Details from one or many `Error`s
  (multiple errors become validation problem details with an `errors` dictionary per field)
- `KotoHttpErrorOptions` — extensible `Error.Code` → HTTP status registry

## Quick start

```csharp
builder.Services.AddKotoAspNetCore(); // optional: o => o.Map("payments.gateway-failed", 502)
```

Minimal API:

```csharp
app.MapPost("/orders", (CreateOrderCommand cmd, ICqrsDispatcher dispatcher, HttpContext ctx, CancellationToken ct)
    => dispatcher.SendAsync(cmd, ct).ToHttpResultAsync(ctx));
```

MVC controller:

```csharp
[HttpPost]
public async Task<ActionResult<OrderDto>> Create(CreateOrderCommand cmd, CancellationToken ct)
    => (await _dispatcher.SendAsync(cmd, ct)).ToActionResult(this);
```

## Status code mapping

Resolution order: exact code → custom rules → suffix → prefix → field-error default → fallback.

| Rule | Status |
|---|---|
| `*.not-found` | 404 |
| `*.already-*`, `*.conflict` | 409 |
| `*.unauthorized` | 401 |
| `*.forbidden` | 403 |
| `general.*`, `validation.*`, `Error.Field != null` | 400 |
| everything else | **422** (fallback, configurable) |

Unmapped business errors are **422, never 500** — a failed `Result` is a rule violation
the client can act on; 500 is reserved for unhandled exceptions.

Customize:

```csharp
builder.Services.AddKotoAspNetCore(o => o
    .Map("subscription.payment-failed", StatusCodes.Status502BadGateway) // exact, highest priority
    .MapSuffix(".expired", StatusCodes.Status410Gone)
    .MapPrefix("quota.", StatusCodes.Status429TooManyRequests)
    .Map(e => e.Code.Contains(".locked-") ? StatusCodes.Status423Locked : null));
```
