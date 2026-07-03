# Koto.Api.AspNetCore — Plan

**Phase:** 3 | **Status:** DONE (v0.3.0-preview.1)
**Depends on:** Koto.Domain + FrameworkReference Microsoft.AspNetCore.App

Решение: [ADR-020](../../adr/ADR-020-api-aspnetcore-error-mapping.md)

---

## Цель

Транспорт-независимый Result→HTTP слой: маппинг `Result<T>` в ответы Minimal API,
MVC и FastEndpoints из одного места. Расширяемый registry `Error.Code` → HTTP-статус.
Незамапленные бизнес-коды — **422**, не 500 (500 зарезервирован за необработанными
исключениями).

## Состав

- [x] `KotoHttpErrorOptions` — registry код → статус: `Map(exact)`, `Map(Func<Error,int?>)`,
      `MapSuffix`, `MapPrefix`, `FallbackStatusCode` (422). Приоритет: exact → custom →
      suffix → prefix → `Field != null` → 400 → fallback. Пользовательские правила
      приоритетнее встроенных.
- [x] Встроенная таблица: `.not-found`→404, `.already-*`/`.conflict`→409,
      `.unauthorized`→401, `.forbidden`→403, `general.*`/`validation.*`→400.
- [x] `KotoProblemDetails` — RFC 7807: одна ошибка → `ProblemDetails` (`errorCode`,
      `field`, `correlationId` extensions); несколько → `HttpValidationProblemDetails`
      (errors по `Error.Field` + `errorCodes`).
- [x] `ToHttpResult` / `ToHttpResultAsync` — `Result<T>` → `IResult` (200/204/problem).
- [x] `ToActionResult` / `ToActionResultAsync` — `Result<T>` → `ActionResult<T>` (MVC).
- [x] `AddKotoAspNetCore(Action<KotoHttpErrorOptions>?)` — регистрация в DI;
      без регистрации работают встроенные дефолты (`GetKotoHttpErrorOptions()`).
- [x] `Koto.Api.FastEndpoints` переиспользует пакет (собственный `KotoProblemDetails` удалён).

## Использование

```csharp
// MVC:
return (await _dispatcher.SendAsync(cmd, ct)).ToActionResult(this);

// Minimal API:
app.MapPost("/orders", (CreateOrderCommand cmd, ICqrsDispatcher d, HttpContext ctx, CancellationToken ct)
    => d.SendAsync(cmd, ct).ToHttpResultAsync(ctx));

// Кастомный маппинг:
builder.Services.AddKotoAspNetCore(o => o
    .Map("subscription.payment-failed", StatusCodes.Status502BadGateway)
    .MapPrefix("quota.", StatusCodes.Status429TooManyRequests));
```
