# Koto.Api.FastEndpoints — Plan

**Phase:** 3 | **Status:** DONE (mapped endpoints + claims helper — v0.2.0-preview.1)
**Depends on:** Koto.Application + **Koto.Api.AspNetCore** + FastEndpoints (MIT)

> **Обновление v0.3.0-preview.1:** `KotoProblemDetails` переехал в транспорт-независимый
> пакет [Koto.Api.AspNetCore](14-api-aspnetcore.md) (ADR-020); захардкоженный
> `StatusCodeFrom` заменён расширяемым `KotoHttpErrorOptions` (незамапленные коды → 422,
> не 500); `AddKotoApi(configureErrors)`; эндпоинты отдают все ошибки `Result.Errors`
> (multi-error → validation problem details).

---

## Цель

Base endpoint классы для CQRS. Каждый endpoint диспатчит команду или запрос через `ICqrsDispatcher`. Problem Details (RFC 7807) из `Error`. Correlation ID propagation.

## Checklist

### Base Endpoints
- [ ] `CommandEndpoint<TCommand>` — base для команд без возвращаемого значения:
  - Диспатчит `TCommand` через `ICqrsDispatcher`
  - При `IsFailure` → `KotoProblemDetails` с 4xx/5xx статусом
  - При `IsSuccess` → 204 No Content

- [ ] `CommandEndpoint<TCommand, TResult>` — base для команд с результатом:
  - При `IsSuccess` → 200 с `TResult`

- [ ] `QueryEndpoint<TQuery, TResult>` where TQuery : IQuery<TResult>:
  - Диспатчит query, возвращает результат
  - При failure с кодом `*.not-found` → 404, иначе → 400/500

### Problem Details
- [ ] `KotoProblemDetails` — RFC 7807 factory из `Error`:
  - Маппинг кодов на HTTP статус: `*.not-found` → 404, `*.already-*` → 409, `general.value.*` → 400, иначе → 500
  - Включает `Error.Code` как `extensions["errorCode"]`
  - Включает `CorrelationId` как `extensions["correlationId"]`

### Middleware
- [ ] `CorrelationIdMiddleware` — ASP.NET Core middleware:
  - Читает `X-Correlation-ID` из request header (или генерирует новый)
  - Кладёт в `AsyncLocal<string>` через `ICorrelationIdAccessor`
  - Добавляет в response header

- [ ] `ICorrelationIdAccessor` — интерфейс: `string? Current { get; }`
- [ ] `GlobalExceptionHandler` — `IExceptionHandler` (ASP.NET Core 8+): ловит unhandled exceptions → Problem Details 500

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoApi(services)`:
  - Регистрирует `CorrelationIdMiddleware`
  - Регистрирует `GlobalExceptionHandler`
  - Регистрирует `ICorrelationIdAccessor`

## Пример endpoint

```csharp
public class PlaceOrderEndpoint : CommandEndpoint<PlaceOrderCommand, OrderId>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        var command = new PlaceOrderCommand(req.CustomerId, req.Items);
        await SendCommandAsync(command, ct);
    }
}
```

## Тесты
- [ ] CommandEndpoint: 204 при Success, ProblemDetails при Failure
- [ ] QueryEndpoint: 200 с результатом, 404 при not-found error
- [ ] KotoProblemDetails: правильный HTTP статус из Error.Code
- [ ] CorrelationId: читается из header или генерируется, добавляется в response

---

## Mapped endpoints — разделение request DTO и команды (v0.2)

`CommandEndpoint<TCommand[, TResult]>` использует `TCommand` одновременно как HTTP request DTO
и как команду. Это удобно, когда тело запроса == команда. Но если команда несёт **server-derived**
поля (`UserId` из JWT, `TenantId`, route id, correlation id), эти поля попадают в контракт запроса
и становятся bindable из тела — клиент может подменить `UserId`/`JudgeId`. Перезаписать их в
`HandleAsync` можно, но поле всё равно видно в OpenAPI и легко забыть.

Для этого случая — отдельное семейство, которое разделяет request DTO и команду через override:

- `MappedCommandEndpoint<TRequest, TCommand, TResult>` — `ToCommand(TRequest)` → 200 с `TResult`
- `MappedCommandEndpoint<TRequest, TCommand>` — void → 204
- `MappedQueryEndpoint<TRequest, TQuery, TResult>` — `ToQuery(TRequest)` → 200

> Имя `Mapped*`, а не `CommandEndpoint<TRequest, TCommand>`: 2-арный слот уже занят
> `CommandEndpoint<TCommand, TResult>` — переиспользование имени не скомпилируется.

`HandleAsync` реализован в базе (`sealed`): диспатчит результат `ToCommand`/`ToQuery` и мапит
`Result` в ответ (та же логика success/ProblemDetails, что и у `CommandEndpoint`).

```csharp
public sealed record SubmitJudgmentRequest(Guid SubmissionId, int GoeScore, string? Comment); // без JudgeId
public sealed record SubmitJudgmentCommand(Guid JudgeId, Guid SubmissionId, int GoeScore, string? Comment)
    : ICommand<Judgment>;

public sealed class SubmitJudgmentEndpoint
    : MappedCommandEndpoint<SubmitJudgmentRequest, SubmitJudgmentCommand, Judgment>
{
    public override void Configure() { Post("/api/v1/judgments"); Policies("IsJudge"); }

    protected override SubmitJudgmentCommand ToCommand(SubmitJudgmentRequest r) =>
        new(JudgeId: User.GetUserId(), r.SubmissionId, r.GoeScore, r.Comment); // JudgeId из claims
}
```

### `ClaimsPrincipal.GetUserId()`
Helper в `Koto.Api.FastEndpoints.Extensions` для частого чтения claim:
- `Guid GetUserId(this ClaimsPrincipal)` — читает `ClaimTypes.NameIdentifier`, бросает при отсутствии/невалидном GUID.
- `bool TryGetUserId(this ClaimsPrincipal, out Guid)` — без исключений.

### Тесты (v0.2)
- [x] `MappedCommandEndpoint`/`MappedQueryEndpoint`: `ToCommand`/`ToQuery` берёт user id из claims, не из request.
- [x] `ClaimsPrincipalExtensions`: парсит `NameIdentifier`; бросает при отсутствии/невалидном GUID; `TryGetUserId` false-path.
