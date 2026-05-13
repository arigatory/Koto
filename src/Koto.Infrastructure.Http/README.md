# Koto.Infrastructure.Http

Anti-Corruption Layer for synchronous HTTP calls between services. Part of the [Koto](https://github.com/arigatory/Koto) suite.

## Install

```bash
dotnet add package Koto.Infrastructure.Http
```

## What's included

| Type | Purpose |
|---|---|
| `ServiceHttpClient` | Abstract base for typed HTTP clients; maps HTTP errors to `Result<T>` |
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

### Error mapping

| HTTP status | `Error.Code` |
|---|---|
| 404 | `general.not-found` |
| 409 | `general.conflict` |
| 422 | `general.validation` |
| 5xx | `general.unexpected` |

Override `MapErrorResponse` to add service-specific error codes.
