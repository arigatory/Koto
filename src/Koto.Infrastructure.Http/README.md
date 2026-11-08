# Koto.Infrastructure.Http

Anti-Corruption Layer for synchronous HTTP calls between services. Part of the [Koto](https://github.com/arigatory/Koto) suite.

## Install

```bash
dotnet add package Koto.Infrastructure.Http
```

## What's included

| Type | Purpose |
|---|---|
| `ServiceHttpClient` | Abstract base for typed HTTP clients; reads Koto envelopes and maps other HTTP errors to `Result<T>` |
| `RemoteError` | An error returned by another Koto service (code, message, field, details) |
| `HttpErrors` | Catalog for non-envelope responses (`general.resource.not-found`, …) |
| `ICorrelationIdAccessor` | Interface for providing the current correlation ID |
| `CorrelationIdHandler` | Delegating handler that propagates `X-Correlation-ID` |
| `AddServiceHttpClient<TInterface, TImplementation>()` | Registers with standard resilience (retry + circuit breaker + timeout) |

## Usage

### 1 — Define the application interface (no HTTP here)

```csharp
// Application layer
public interface IPaymentService
{
    Task<Result<PaymentId>> ChargeAsync(Money amount, CancellationToken ct);
}
```

### 2 — Implement with the HTTP client

```csharp
// Infrastructure layer
public class PaymentServiceHttpClient : ServiceHttpClient, IPaymentService
{
    public PaymentServiceHttpClient(HttpClient http) : base(http) { }

    public async Task<Result<PaymentId>> ChargeAsync(Money amount, CancellationToken ct)
    {
        var response = await Http.PostAsJsonAsync("/charges",
            new { Amount = amount.Amount, Currency = amount.Currency }, ct);
        return await ReadResultAsync<PaymentId>(response, ct);
    }
}
```

### 3 — Register

```csharp
// Implement ICorrelationIdAccessor (typically wraps IHttpContextAccessor):
services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();

services.AddServiceHttpClient<IPaymentService, PaymentServiceHttpClient>(
    name: "payment-service",
    baseUrl: config["Services:Payment:BaseUrl"]!);
```

Standard resilience pipeline is applied automatically (3 retries with exponential back-off, circuit breaker, 30 s timeout).

### Reading responses

`ReadResultAsync<T>` returns a domain `Result<T>` — the calling handler enriches it like any other
domain result (`ForField`, `Enrich`).

- A Koto **envelope** body is unwrapped: `result` on success, each entry of `errors` as a
  `RemoteError` (the remote `code`, `message`, `field` and `details`) on failure. Domain codes cross the
  service boundary intact, so `if (result.FirstError.Code == "wallet.account.insufficient-funds")`
  works without parsing JSON by hand. A malformed remote code becomes `general.remote.unknown-error`
  with the original code in `details.remoteCode`.
- Any other body: deserialized as `T` on a success status, otherwise mapped through `MapErrorResponse`:

| HTTP status | Error |
|---|---|
| 404 | `HttpErrors.ResourceNotFound` — `general.resource.not-found` |
| 409 | `HttpErrors.ResourceConflict` — `general.resource.conflict` |
| 400 / 422 | `HttpErrors.RequestInvalid` — `general.request.invalid` (message = response body) |
| 5xx | `HttpErrors.ServerUnexpected` — `general.server.unexpected` (`details.statusCode`) |
| other | `HttpErrors.HttpFailure` — `general.server.http-error` |

Override `MapErrorResponse` to add service-specific errors.

## Service-to-service auth

```csharp
// calling side
services.AddServiceHttpClient<IWalletService, WalletClient>(
    "wallet", "http://wallet", o => o.ApiKey = configuration["ServiceAuth:Key"]!);

// receiving side (Koto.Api.AspNetCore)
services.AddAuthentication().AddServiceKey(configuration["ServiceAuth:Key"]!);
// internal endpoint: Roles("Service")
```

## Upgrading from 0.4

0.5 unwraps the Koto envelope of the remote service into `RemoteError`s that keep the remote code, and moves the fallback errors for non-envelope responses into `HttpErrors` with new codes (`general.conflict` → `general.resource.conflict`, …). The step-by-step guide for the whole suite, with before/after code, is
[docs/migrations/0.4-to-0.5.md](https://github.com/arigatory/Koto/blob/main/docs/migrations/0.4-to-0.5.md).
