# Koto.Infrastructure.Http — Plan

**Phase:** 2 | **Status:** NOT STARTED
**Depends on:** Koto.Domain + Microsoft.Extensions.Http.Resilience (Polly v8)

---

## Цель

Anti-Corruption Layer для синхронных HTTP-вызовов к другим сервисам. Application layer определяет только интерфейс — без знания о HTTP. Infrastructure реализует через типизированный HttpClient.

## Паттерн использования

```csharp
// Application layer (Koto.Domain / Application):
public interface IPaymentService
{
    Task<Result<PaymentId, Error>> ChargeAsync(Money amount, CancellationToken ct);
}

// Infrastructure layer (реализация):
public class PaymentServiceHttpClient : ServiceHttpClient, IPaymentService
{
    public PaymentServiceHttpClient(HttpClient http) : base(http) { }

    public async Task<Result<PaymentId, Error>> ChargeAsync(Money amount, CancellationToken ct)
    {
        var response = await Http.PostAsJsonAsync("/charges", new { Amount = amount.Amount, Currency = amount.Currency }, ct);
        return await ReadResultAsync<PaymentId>(response, ct);
    }
}

// Registration:
services.AddServiceHttpClient<IPaymentService, PaymentServiceHttpClient>(
    name: "payment-service",
    baseUrl: config["Services:Payment:BaseUrl"]);
```

## Checklist

### ServiceHttpClient
- [ ] `ServiceHttpClient` — abstract base class:
  - `protected HttpClient Http { get; }` — инжектируется через конструктор
  - `protected Task<Result<T, Error>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken ct)` — десериализует успешный ответ или маппирует ошибку
  - `protected virtual Error MapErrorResponse(HttpResponseMessage response, string? body)` — виртуальный, можно переопределить:
    - 404 → `Errors.General.NotFound(...)`
    - 409 → Conflict error
    - 422 → Validation error (парсит body)
    - 5xx → Unexpected error
  - Автоматически добавляет `X-Correlation-ID` header из `ICorrelationIdAccessor`

### DI Registration
- [ ] `ServiceCollectionExtensions.AddServiceHttpClient<TInterface, TImplementation>(services, name, baseUrl)`:
  - Регистрирует `HttpClient` с `AddStandardResilienceHandler()` (retry + circuit breaker + timeout)
  - Устанавливает `BaseAddress`
  - Регистрирует `TImplementation` как `TInterface` в DI

## Тесты
- [ ] 200 ответ → Success result с десериализованным значением
- [ ] 404 → Failure с `general.entity.not-found`
- [ ] 5xx → Failure с unexpected error
- [ ] X-Correlation-ID пробрасывается в заголовок
- [ ] Resilience: retry срабатывает при transient failures (mock HttpMessageHandler)
