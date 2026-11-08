# Error и Result — соглашения Koto (v0.5)

Решения: [ADR-026](../../adr/ADR-026-error-model-and-envelope.md), спека [16](../packages/16-error-model-envelope.md).

## Три слоя, два результата, один конверт

| Слой | Возвращает | Ошибки |
|---|---|---|
| Domain | `Result<T>` | `Error` — record из каталога |
| Application (хендлеры, диспетчер) | `AppResult<T>` | `EnrichedError` |
| API / транспорт | `Envelope<T>` | `EnvelopeError`: `code`, `message`, `field`, `details` |

Неявной конверсии `Result<T>` → `AppResult<T>` нет. Исключения между слоями не летают: отказ едет как значение.

## Error — абстрактный record, каталог — типы

```csharp
public abstract record Error(string Code, string Message);   // Koto.Domain

// Identity.Domain/Errors/OtpErrors.cs
public sealed record OtpExpired(DateTimeOffset ExpiredAt)
    : Error("identity.otp.expired", "Код истёк.");
public sealed record OtpTooManyAttempts(int RetryAfterSeconds)
    : Error("identity.otp.too-many-attempts", "Слишком много попыток.");
```

- `new Error("…", "…")` невозможен. Record определяется в слое, который обнаружил условие: домен, Application, инфраструктура.
- Свойства record — **гарантированные данные** ошибки; на проводе они становятся `details` (`{ "retryAfterSeconds": 42 }`).
- Код валидируется при создании: `^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*){2,}$` — не меньше трёх kebab-case сегментов, `контекст.сущность.проблема`.
- Общие ошибки — `new Errors.General.ValueIsRequired()`, `InvalidLength(min, max)`, `NotFound(entity, id)`, `CollectionIsTooSmall(min, actual)`, `CollectionIsTooLarge(max, actual)`; коды `general.value.is-required`, `general.value.invalid-length`, `general.entity.not-found`, `general.collection.too-small`, `general.collection.too-large`.
- Каталог проверяется тестом: `ErrorCatalog.ShouldBeValid(typeof(Order).Assembly)` из `Koto.Testing` — формат кодов, уникальность, конструируемость.

## Result\<T\> (домен)

```csharp
public sealed class Result<T> : IResultBase
{
    bool IsSuccess; bool IsFailure;
    T Value;                       // бросает на failure
    ErrorList<Error> Errors;      // все ошибки; пустой на success
    Error FirstError;             // заголовок отказа; бросает на success
    // Success / Failure(Error) / Failure(IEnumerable<Error>) / Failure(ErrorList<Error>)
    // Map, Bind, Tap, TapErrors, Ensure, Match(onSuccess, onFailure: ErrorList), async-версии
}
public static class Result { Success(); Failure(...); Combine(...); Sequence(...); Traverse(...); }
```

Неявные конверсии: `T → Result<T>`, `Error → Result<T>`, `ErrorList<Error> → Result<T>`. Поэтому проброс пишется так:

```csharp
var email = Email.Create(value);
if (email.IsFailure) return email.Errors;      // все ошибки
// return email.FirstError; — так НЕ пробрасывают: из N ошибок уедет одна
```

## EnrichedError и три глагола (Application)

```csharp
public record EnrichedError(Error Inner) : Error(Inner.Code, Inner.Message)
{
    string? Field { get; init; }                                 // поле запроса
    IReadOnlyDictionary<string, object?> Details { get; init; }  // необязательные дополнения
}
```

Домен не знает ни `Field`, ни `Details`. Из хендлера доменная ошибка выходит одним из трёх явных глаголов:

```csharp
if (email.IsFailure)   return email.Errors.ForField(nameof(cmd.Email));   // привязать к полю
if (debit.IsFailure)   return debit.Errors.Enrich();                       // добавить нечего (сказано явно)
if (joined.IsFailure)  return joined.Errors.Enrich(e => e is MeetupFull f  // более полный типизированный record
                            ? new MeetupFullWithAlternative(f, alternative.Id)
                            : e.Enrich());
```

Гарантированные данные из базы — тоже свойства record, но уже Application-слоя:

```csharp
public sealed record MeetupFullWithAlternative(MeetupFull Cause, Guid AlternativeMeetupId) : EnrichedError(Cause);
public sealed record EmailTaken(string Email, Guid ExistingUserId) : Error("identity.email.taken", "…");  // ошибка, обнаруженная Application
```

Правила: гарантированные данные живут в свойствах record того слоя, который ошибку обнаружил; словарь `Details` — только для необязательного; `Enrich()` без аргументов — осознанное «данные уже полные».

## AppResult\<T\>

Форма та же, что у `Result<T>`, но `Errors : ErrorList<EnrichedError>` и `FirstError : EnrichedError`. Неявные конверсии только из `T`, `EnrichedError`, `ErrorList<EnrichedError>`. `Result<T>.Enrich()` переводит весь результат в `AppResult<T>`. API-слой (`ToHttpResult`, `ToActionResult`, эндпоинты FastEndpoints) принимает **только** `AppResult<T>`.

## Envelope — один формат на все транспорты

```json
{ "success": false, "result": null,
  "errors": [ { "code": "identity.otp.too-many-attempts", "message": "Слишком много попыток.",
                "field": null, "details": { "retryAfterSeconds": 42 } } ],
  "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:09Z" }
```

- `success` — дискриминатор; `result` на отказе всегда `null` (и для struct-результатов); `errors` всегда массив; `field` в camelCase по сегментам; `timestamp` — UTC из `TimeProvider`.
- Успех тоже в конверте (`200`), void-команды — `200` с `result: null`. Не-JSON ответы (файлы, SSE) вне конверта.
- `Envelope<T>` живёт в `Koto.Application`: SignalR-хаб или Kafka reply возвращают тот же тип.
- HTTP-статус — грубый класс для инфраструктуры, не второй код: `*.not-found` 404, `*.already-*`/`*.conflict` 409, `*.unauthorized` 401, `*.forbidden` 403, `general.*`/`validation.*`/привязано к полю 400, остальное 422, исключение 500. При нескольких ошибках статус задаёт первая (`Combine` сохраняет порядок). Переопределение: `AddKotoApi(o => o.Map("payments.gateway.failed", 502))`.

## Гарантии контракта «код → данные»

1. Компилятор: обязательные данные — параметры record, без них ошибку не создать.
2. `EnrichmentGuard` на границе API: если для типа ошибки существует более полный record (производный от `EnrichedError` с ctor-параметром этого типа), а на провод идёт голый экземпляр — исключение в Development, предупреждение в проде.
3. `ErrorCatalog.ShouldBeValid` в тестах: формат, уникальность кодов, конструируемость.

## Исключения

Только для неожиданного: баг, упавшая база, таймаут. Их ловит один `GlobalExceptionHandler` и отдаёт тот же конверт с `general.server.internal-error` и 500. Ожидаемый исход (конфликт версий, занятый слот, лимит) — всегда `Result`, никогда исключение.

## Нет Maybe\<T\>

```csharp
Customer? customer = await repo.GetByIdAsync(id, ct);
if (customer is null)
    return new Errors.General.NotFound("customer", id).ForField("customerId");
```
