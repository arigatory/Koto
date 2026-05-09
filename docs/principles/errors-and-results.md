# Error и Result — Соглашения Koto

## Error

```csharp
public sealed record Error(string Code, string Message)
{
    public string Serialize() => $"{Code}::{Message}";
}
```

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

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }        // throws if IsFailure
    public Error Error { get; }    // throws if IsSuccess

    // Фабрика:
    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    // Функциональная композиция:
    public Result<TNew> Map<TNew>(Func<T, TNew> map);
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> bind);
    public Result<T> Tap(Action<T> action);
    public Result<T> TapError(Action<Error> action);
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure);
    public Result<T> Ensure(Func<T, bool> predicate, Error error);

    // Async overloads для всех методов выше
}
```

### Паттерны использования

**В фабричном методе Value Object:**
```csharp
public static Result<Email, Error> Create(string value)
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

**В FastEndpoints endpoint:**
```csharp
var result = await _dispatcher.SendAsync(command, ct);
if (result.IsFailure)
    return await SendErrorAsync(result.Error, ct);
return await SendOkAsync(result.Value, ct);
```

## Нет Maybe\<T\>

Используем C# nullable reference types:
```csharp
// Вместо Maybe<Customer>:
Customer? customer = await _repo.GetByIdAsync(id, ct);
if (customer is null)
    return Errors.General.NotFound("customer", id);
```

Это идиоматично для C# 8+ и не требует дополнительного обучения.
