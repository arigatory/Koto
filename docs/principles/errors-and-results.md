# Error и Result — Соглашения Koto

## Error

```csharp
public sealed record Error(string Code, string Message)
{
    // Имя поля/свойства, к которому относится ошибка. Заполняет application-слой
    // (validation pipeline); доменные фабрики обычно оставляют null.
    // Используется HTTP-слоем для validation problem details.
    public string? Field { get; init; }
}
```

> `Serialize()` удалён в v0.3.0 — это был транспортный хак для FluentValidation v7.
> Структурный `Error` теперь передаётся через `ValidationFailure.CustomState` (см. ревизию ADR-009).

### Формат кода ошибки

```
{сервис-или-general}.{сущность-или-раздел}.{описание}
```

Примеры:
```
general.value.is-required
general.value.invalid-length
general.entity.not-found
orders.order.already-cancelled
orders.order-item.quantity-exceeds-stock
payments.charge.insufficient-funds
```

- Все части в kebab-case, точка как разделитель уровней.
- `general.*` — ошибки, которые может использовать любой сервис (определены в `Errors.General`).
- `{сервис}.*` — доменные ошибки конкретного сервиса, определяются рядом с доменом этого сервиса.

### Errors.General

```csharp
public static class Errors
{
    public static class General
    {
        public static Error ValueIsRequired(string? field = null) =>
            new("general.value.is-required", field is null
                ? "Value is required."
                : $"'{field}' is required.");

        public static Error InvalidLength(int min, int max, string? field = null) =>
            new("general.value.invalid-length",
                $"Length must be between {min} and {max}.");

        public static Error NotFound(string field, object? id = null) =>
            new("general.entity.not-found", id is null
                ? $"'{field}' was not found."
                : $"'{field}' with id '{id}' was not found.");

        public static Error CollectionIsTooSmall(int min, int actual) =>
            new("general.collection.too-small",
                $"Collection must contain at least {min} items. Actual: {actual}.");

        public static Error CollectionIsTooLarge(int max, int actual) =>
            new("general.collection.too-large",
                $"Collection must contain at most {max} items. Actual: {actual}.");
    }
}
```

## Result\<T\>

Собственная реализация, без внешних зависимостей. Вдохновлена Khorikov.
С v0.3.0 — multi-error: failure несёт одну или несколько ошибок.

```csharp
public sealed class Result<T> : IResultBase, IResultFactory<Result<T>>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }                     // throws if IsFailure
    public Error Error { get; }                 // первая ошибка; throws if IsSuccess
    public IReadOnlyList<Error> Errors { get; } // все ошибки; пустой список на success

    // Фабрики (null-guards: Success(null) и Failure(пустая коллекция) бросают):
    public static Result<T> Success(T value);
    public static Result<T> Failure(Error error);
    public static Result<T> Failure(IEnumerable<Error> errors);

    // Функциональная композиция (Map/Bind пропагируют ВСЕ ошибки):
    public Result<TNew> Map<TNew>(Func<T, TNew> map);
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> bind);
    public Result<T> Tap(Action<T> action);
    public Result<T> TapError(Action<Error> action);          // первая ошибка
    public Result<T> TapErrors(Action<IReadOnlyList<Error>>); // все ошибки
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure);
    public Task<TResult> MatchAsync<TResult>(...);            // async success + async/sync failure
    public Result<T> Ensure(Func<T, bool> predicate, Error error);

    // Async overloads для всех методов выше
}

// Статический компаньон для void-потоков и агрегации:
public static class Result
{
    public static Result<Unit> Success();
    public static Result<Unit> Failure(Error error);
    public static Result<Unit> Failure(IEnumerable<Error> errors);

    // Combine собирает ВСЕ ошибки (не первую), на успехе — кортеж значений:
    public static Result<(T1, T2)> Combine<T1, T2>(Result<T1> r1, Result<T2> r2); // arity 2–4
    public static Result<Unit> Combine(params IResultBase[] results);
}
```

### Паттерны использования

**В фабричном методе Value Object:**
```csharp
public static Result<Email> Create(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return Errors.General.ValueIsRequired("email");
    if (value.Length > 150)
        return Errors.General.InvalidLength(1, 150, "email");
    return new Email(value);
}
```

**В Command Handler:**
```csharp
public async Task<Result<OrderId>> Handle(CreateOrderCommand cmd, CancellationToken ct)
{
    var customerResult = await _customerRepo.GetByIdAsync(cmd.CustomerId, ct);
    if (customerResult is null)
        return Errors.General.NotFound("customer", cmd.CustomerId);

    var order = Order.Create(customerResult, cmd.Items);
    _orderRepo.Add(order);
    await _db.SaveChangesAsync(ct);

    return order.Id;
}
```

**Цепочка (pipeline):**
```csharp
return await Result<string>.Success(rawEmail)
    .Bind(Email.Create)
    .Map(email => new RegisteredUser(email))
    .TapAsync(user => _repo.AddAsync(user, ct));
```

**Агрегация нескольких фабрик (все ошибки, не первая):**
```csharp
var combined = Result.Combine(Email.Create(dto.Email), Name.Create(dto.Name));
if (combined.IsFailure)
    return Result<User>.Failure(combined.Errors);
var (email, name) = combined.Value;
```

**В FastEndpoints endpoint:**
```csharp
var result = await _dispatcher.SendAsync(command, ct);
if (result.IsFailure)
    return await SendErrorAsync(result.Error, ct);
return await SendOkAsync(result.Value, ct);
```

**В MVC / Minimal API (Koto.Api.AspNetCore):**
```csharp
// MVC-контроллер:
return (await _dispatcher.SendAsync(command, ct)).ToActionResult(this);

// Minimal API:
app.MapPost("/orders", (CreateOrderCommand cmd, ICqrsDispatcher d, HttpContext ctx, CancellationToken ct)
    => d.SendAsync(cmd, ct).ToHttpResultAsync(ctx));
```
Маппинг `Error.Code` → HTTP-статус — через расширяемый `KotoHttpErrorOptions`
(ADR-020): незамапленные бизнес-коды дают **422**, не 500.

## Нет Maybe\<T\>

Используем C# nullable reference types:
```csharp
// Вместо Maybe<Customer>:
Customer? customer = await _repo.GetByIdAsync(id, ct);
if (customer is null)
    return Errors.General.NotFound("customer", id);
```

Это идиоматично для C# 8+ и не требует дополнительного обучения.
