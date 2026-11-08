# 16 — Модель ошибок и конверт ответа (v0.5.0-preview.1)

Статус: **в работе**. Решения зафиксированы в [ADR-026](../../adr/ADR-026-error-model-and-envelope.md);
ревизии [ADR-002](../../adr/ADR-002-own-result-implementation.md), [ADR-003](../../adr/ADR-003-error-type-design.md),
[ADR-020](../../adr/ADR-020-api-aspnetcore-error-mapping.md) (superseded).

## Цели

1. Ошибка едет от агрегата до клиента **как значение** по одному рельсу (`Result` → `AppResult` → `Envelope`);
   исключения не пересекают слои и остаются только для неожиданного (баг, инфраструктура).
2. **Все** ошибки отказа доходят до клиента, а не первая: проброс `return x.Errors;` так же короток, как
   прежний `return x.Error;`, и сохраняет список.
3. Каждая ошибка несёт машиночитаемые данные (`details`) с контрактом «код → обязательные поля», проверяемым
   компилятором в точке создания и каталогом на границе API.
4. Один формат ответа для всех транспортов (HTTP, SignalR, Kafka reply): `Envelope<T>`.
5. Домен чист: ни `Field`, ни `Details`, ни HTTP в `Koto.Domain`.

## Не-цели

- Автоматическое обогащение ошибок: Application делает это явно (`ForField`, `Enrich`, типизированный record).
- Compile-time гарантия, что хендлер применил *самый полный* тип для кода, — это проверка на границе (dev/test).
- Генерация TypeScript-типов из каталога ошибок — отдельная задача (каталог для неё уже машиночитаем).
- Переезд не-JSON ответов (файлы, SSE) в конверт.

## Модель типов по слоям

| Слой | Тип результата | Тип ошибки | Пакет |
|---|---|---|---|
| Domain | `Result<T>` | `Error` (abstract record, каталог record-типов) | Koto.Domain |
| Application | `AppResult<T>` | `EnrichedError : Error` (декоратор: `Inner`, `Field`, `Details`) | Koto.Application |
| API / транспорт | `Envelope<T>` | `EnvelopeError` (плоский DTO: `code`, `message`, `field`, `details`) | Koto.Application (DTO), Koto.Api.* (HTTP) |

`Result<T>` и `AppResult<T>` — разные классы с одинаковой формой; неявной конверсии из `Result<T>` в
`AppResult<T>` **нет**. Переход делает один из трёх явных глаголов Application-слоя.

## Публичный API

### Koto.Domain

```csharp
public abstract record Error(string Code, string Message);          // Code валидируется: ErrorCode.Validate
public static class ErrorCode { static bool IsValid(string); static string Validate(string); }
// формат: ≥3 сегмента kebab-case через точку: ^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*){2,}$

public static class Errors { public static class General {
    sealed record ValueIsRequired()                    : Error("general.value.is-required", …);
    sealed record InvalidLength(int Min, int Max)      : Error("general.value.invalid-length", …);
    sealed record NotFound(string Entity, object? Id = null) : Error("general.entity.not-found", …);
    sealed record CollectionIsTooSmall(int Min, int Actual)  : Error("general.collection.too-small", …);
    sealed record CollectionIsTooLarge(int Max, int Actual)  : Error("general.collection.too-large", …);
}}

public readonly struct ErrorList<TError> : IReadOnlyList<TError> where TError : Error
{   // неизменяемый список; default == пустой
    ErrorList(IEnumerable<TError>); static ErrorList<TError> Empty; int Count; TError this[int];
    ErrorList<TOut> Select<TOut>(Func<TError, TOut>) where TOut : Error;
    static implicit operator ErrorList<TError>(TError);
}

public sealed class Result<T> : IResultBase
{
    bool IsSuccess; bool IsFailure; T Value;                       // Value бросает на failure
    ErrorList<Error> Errors; Error FirstError;                    // FirstError бросает на success
    static Result<T> Success(T); Failure(Error); Failure(IEnumerable<Error>);
    implicit: T → Result<T>; Error → Result<T>; ErrorList<Error> → Result<T>;
    Map, Bind, Tap, TapErrors(Action<ErrorList<Error>>), Ensure,
    Match(Func<T,R>, Func<ErrorList<Error>,R>), MatchAsync ×2, MapAsync, BindAsync, TapAsync, EnsureAsync
}
public static partial class Result { Success(); Failure(Error); Failure(IEnumerable<Error>); Combine ×4; Sequence; Traverse ×3 }
public interface IResultBase { bool IsSuccess; bool IsFailure; IReadOnlyList<Error> Errors; }
```

Удалено: `Error.Field`, `Result<T>.Error`, `TapError`, `IResultFactory<TSelf>`, `IDomainService`.

### Koto.Application

```csharp
public record EnrichedError : Error
{
    Error Inner;                                   // исходная ошибка; двойная обёртка схлопывается
    string? Field { get; init; }
    IReadOnlyDictionary<string, object?> Details { get; init; }   // необязательные дополнения
    EnrichedError(Error inner);                    // Code/Message копируются из inner
}
public static class ErrorEnrichment               // extension methods
{
    EnrichedError Enrich(this Error);  EnrichedError ForField(this Error, string field);
    EnrichedError WithDetails(this Error, params (string Key, object? Value)[]);
    ErrorList<EnrichedError> Enrich(this ErrorList<Error>);
    ErrorList<EnrichedError> Enrich(this ErrorList<Error>, Func<Error, EnrichedError> map);
    ErrorList<EnrichedError> ForField(this ErrorList<Error>, string field);
    Result<T>    ForField<T>(this Result<T>, string field);   // остаётся Result<T> (для Combine)
    AppResult<T> Enrich<T>(this Result<T>);                    // финальный переход в Application-результат
}
public static class ErrorDetails { IReadOnlyDictionary<string, object?> Of(Error error); }
// плоские details: свойства всей цепочки record'ов (camelCase) + словарь Details; служебные
// свойства (Code, Message, Field, Details, Inner, EqualityContract) и свойства типа Error исключены,
// в последние рекурсивно спускаемся; внешние свойства и явный словарь перекрывают внутренние.

public sealed class AppResult<T> : IResultBase, IAppResultFactory<AppResult<T>>
{
    bool IsSuccess; bool IsFailure; T Value; ErrorList<EnrichedError> Errors; EnrichedError FirstError;
    static Success(T); Failure(EnrichedError); Failure(IEnumerable<EnrichedError>);
    implicit: T; EnrichedError; ErrorList<EnrichedError>;
    Map, Match(Func<T,R>, Func<ErrorList<EnrichedError>,R>), MatchAsync ×2;
    Envelope<T> ToEnvelope(DateTimeOffset timestamp, string? correlationId = null);
}
public static class AppResult { Success() → AppResult<Unit>; Failure(EnrichedError); Failure(IEnumerable<EnrichedError>); }
public interface IAppResultFactory<TSelf> { static abstract TSelf FromErrors(IReadOnlyList<EnrichedError>); }

// Конверт — транспорт-независимый DTO (HTTP, SignalR, Kafka reply)
public sealed record EnvelopeError(string Code, string Message, string? Field, IReadOnlyDictionary<string, object?> Details)
{ static EnvelopeError From(EnrichedError); }
public sealed record Envelope<T>(bool Success, T? Result, IReadOnlyList<EnvelopeError> Errors, string? CorrelationId, DateTimeOffset Timestamp);
public static class Envelope
{
    Envelope<T> Ok<T>(T result, DateTimeOffset timestamp, string? correlationId = null);
    Envelope<T> Fail<T>(IReadOnlyList<EnrichedError> errors, DateTimeOffset timestamp, string? correlationId = null);
    Envelope<object?> Fail(IReadOnlyList<EnrichedError> errors, DateTimeOffset timestamp, string? correlationId = null);
}

// CQRS — все контракты возвращают AppResult
ICommandHandler<TCommand>            : Task<AppResult<Unit>>    HandleAsync(TCommand, CancellationToken ct = default)
ICommandHandler<TCommand, TResult>   : Task<AppResult<TResult>> HandleAsync(...)
IQueryHandler<TQuery, TResult>       : Task<AppResult<TResult>> HandleAsync(...)
ICqrsDispatcher                      : SendAsync → AppResult<Unit> / AppResult<TResult>; QueryAsync → AppResult<TResult>

public sealed class KotoApplicationOptions { … bool ValidateHandlers { get; set; } = true; }
// AddKotoApplication при ValidateHandlers бросает InvalidOperationException, перечисляя команды/запросы
// из сканируемых сборок без хендлера или с несколькими.
public sealed class KotoApplicationAssemblies { IReadOnlyList<Assembly> Assemblies; }   // singleton, для каталога ошибок
```

### Koto.Validation

```csharp
public sealed record ValidationRuleFailed : Error { string Rule; ctor(string rule, string message) : base("validation.rule.failed", message) }
public sealed class ValidationBehavior<TRequest, TResponse> where TResponse : IResultBase, IAppResultFactory<TResponse>
// валидаторы выполняются ПОСЛЕДОВАТЕЛЬНО; каждая ValidationFailure → EnrichedError { Field = PropertyName }:
//   CustomState is Error e  → new EnrichedError(e)   (доменная фабрика через MustBeValueObject)
//   иначе                   → new EnrichedError(new ValidationRuleFailed(failure.ErrorCode, failure.ErrorMessage))
public static class KotoApplicationOptionsExtensions { KotoApplicationOptions AddValidationBehavior(this KotoApplicationOptions); }
AddKotoValidation(params Assembly[])   // регистрирует ValidationBehavior<,> только если его ещё нет; сканирует IValidator<T>
KotoValidators: MustBeValueObject, ListMustContainNumberOfItems   // MustBeEntity, NotEmptyWithKotoError, LengthWithKotoError удалены
```

Рекомендуемая регистрация: `AddKotoApplication(o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(), asm)`
+ `AddKotoValidation(asm)`.

### Koto.Api.AspNetCore

```csharp
public sealed class KotoApiOptions
{
    int FallbackStatusCode = 422;
    KotoApiOptions Map(string exactCode, int status); KotoApiOptions Map(Func<Error, int?> rule);
    EnrichmentGuardMode? EnrichmentGuard;          // null → Throw в Development, иначе Warn
    int StatusCodeFor(Error); int StatusCodeFor(IReadOnlyList<Error>);   // список → статус первой ошибки
}
// встроенный классификатор (после exact и rules): *.not-found → 404; *.already-* / *.conflict → 409;
// *.unauthorized → 401; *.forbidden → 403; general.* / validation.* / EnrichedError{Field != null} → 400; иначе Fallback (422).
public enum EnrichmentGuardMode { Off, Warn, Throw }
public sealed class EnrichmentGuard { void Check(IReadOnlyList<EnrichedError> errors); }
// каталог: для каждого типа, производного от EnrichedError, типы его ctor-параметров, производные от Error, —
// «неполные на проводе»; экземпляр такого типа без обёртки → нарушение.

AppResult<T>.ToHttpResult(HttpContext, string? correlationId = null) : IResult        // Envelope<T>, статус по классификатору
AppResult<Unit>.ToHttpResult(...) : IResult                                           // Envelope<object?>, 200
Task<AppResult<T>>.ToHttpResultAsync(...), ToActionResult / ToActionResultAsync (MVC)
AddKotoAspNetCore(Action<KotoApiOptions>? configure = null)   // options + TryAddSingleton(TimeProvider.System) + EnrichmentGuard
```

Удалено: `KotoProblemDetails`, `KotoHttpErrorOptions` (→ `KotoApiOptions`), `MapSuffix`, `MapPrefix`, `AddProblemDetails()`.

### Koto.Api.FastEndpoints

```csharp
CommandEndpoint<TCommand>            : Endpoint<TCommand, Envelope<object?>>
CommandEndpoint<TCommand, TResult>   : Endpoint<TCommand, Envelope<TResult>>
QueryEndpoint<TQuery, TResult>       : Endpoint<TQuery,   Envelope<TResult>>
Mapped* — аналогично. HandleAsync sealed; успех — 200 + Envelope, отказ — статус по KotoApiOptions + Envelope.
app.UseKotoFastEndpoints(Action<Config>? configure = null)   // UseFastEndpoints + ошибки биндинга/валидации FE в том же конверте (400)
GlobalExceptionHandler → Envelope.Fail([ServerError]) + 500; код general.server.internal-error
AddKotoApi(Action<KotoApiOptions>? configureErrors = null)
```

### Koto.Testing

```csharp
Result<T>.Should()    → ResultAssertions<T>:    BeSuccess, BeFailure, BeFailureWith(code), HaveErrors(params codes), HaveValue
AppResult<T>.Should() → AppResultAssertions<T>: то же + HaveErrorForField(field)
public static class ErrorCatalog
{
    IReadOnlyList<ErrorCatalogEntry> Scan(params Assembly[]);     // инстанцирует record'ы заглушками
    void ShouldBeValid(params Assembly[]);                        // формат кодов, уникальность, ctor без исключений
}
public sealed record ErrorCatalogEntry(Type Type, string Code, string Message, IReadOnlyList<string> DetailKeys);
```

### Koto.Infrastructure.Http

```csharp
public sealed record RemoteError : Error { string? Field; IReadOnlyDictionary<string, object?> Details; }
ServiceHttpClient.ReadResultAsync<T>: тело-конверт → Result<T> (ошибки как RemoteError); не конверт → MapErrorResponse
// коды fallback: general.resource.not-found, general.resource.conflict, general.request.invalid,
// general.server.unexpected, general.server.http-error
```

### Koto.Infrastructure.EFCore

- `EfCoreUnitOfWork.CommitAsync`: агрегаты подняли события, а `IDbContextOutbox` не зарегистрирован →
  `InvalidOperationException` с инструкцией (раньше события терялись молча).
- Удалены `ISpecification`, `Specification`, `SpecificationEvaluator`.

## Формат на проводе

```json
{ "success": true,  "result": { "id": "…" }, "errors": [], "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:03Z" }

{ "success": false, "result": null,
  "errors": [ { "code": "identity.otp.too-many-attempts", "message": "Слишком много попыток.",
                "field": null, "details": { "retryAfterSeconds": 42 } } ],
  "correlationId": "0f3c…", "timestamp": "2026-11-08T02:11:09Z" }
```

Правила: HTTP-статусы остаются осмысленными (грубый классификатор); не-JSON ответы вне конверта;
`details` — скаляры и их массивы, ключи camelCase, идентификаторы именуются по сущности (`orderId`).

## Зависимости

Новых пакетов нет. `Koto.Application` получает `Envelope` без внешних зависимостей (System.Text.Json inbox).

## Открытые вопросы (решены)

- Имя типа результата хендлеров: `AppResult<T>`. Имя исходной ошибки в декораторе: `Inner`.
- Версия: `0.5.0-preview.1`; публикация через тег `v0.5.0-preview.1`.
