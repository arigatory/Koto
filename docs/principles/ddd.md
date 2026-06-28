# DDD Принципы Koto

## Агрегаты

- Агрегат — единственная точка изменения состояния для своего bounded context.
- Внешний код никогда не меняет внутренние сущности агрегата напрямую — только через методы агрегата.
- Агрегат должен быть **всегда валидным** (Always Valid Domain Model): невалидное состояние невозможно создать. Валидация происходит в фабричных методах и командах агрегата, не снаружи.
- Агрегат небольшой: охватывает только то, что должно изменяться вместе в одной транзакции.
- Ссылки между агрегатами — только через ID, не через объекты.

```csharp
// Правильно: изменение через метод агрегата
order.AddItem(productId, quantity);

// Неправильно: прямое изменение внутренней коллекции
order.Items.Add(new OrderItem(...));
```

## Доменные события

- Агрегат поднимает события через `AddDomainEvent(new SomethingHappenedEvent(...))`.
- Событие описывает **что произошло**, а не что нужно сделать. Имя — прошедшее время: `OrderPlaced`, `PaymentFailed`.
- Domain events — **внутренний** контракт сервиса. Никогда не публикуются напрямую в Kafka.
- Для межсервисного взаимодействия создаётся отдельный `IIntegrationEvent`.
- Dispatch domain events: через Wolverine outbox — событие сохраняется в БД в той же транзакции с бизнес-данными, затем доставляется асинхронно. Гарантия at-least-once.

## Value Objects

Два подхода, оба допустимы:

**1. record (простой VO — предпочтительный)**
```csharp
public sealed record Email
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsRequired();
        if (value.Length > 150)
            return Errors.General.InvalidLength(1, 150);
        // формат email...
        return new Email(value);
    }
}
```

**2. ValueObject abstract base (когда нужна кастомная equality)**
```csharp
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) { ... }
    public override int GetHashCode() { ... }
}

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency.ToUpperInvariant(); // нормализация при сравнении
    }
}
```

Используй `ValueObject` base только если нужна нормализация или исключение полей из сравнения. В остальных случаях — `record`.

## Репозитории

- Интерфейс репозитория определяется в **домене** (`Koto.Domain`), реализация — в инфраструктуре (`Koto.Infrastructure.EFCore`).
- `Add(aggregate)` и `Delete(aggregate)` — синхронные, только регистрируют намерение в change tracker, не делают I/O.
- `GetByIdAsync(id, ct)` — async, читает из БД.
- **Нет `SaveAsync`** — коммит делает Unit of Work (`DbContext.SaveChangesAsync`) в application layer, не репозиторий.
- Репозиторий работает только с агрегатами, не с отдельными сущностями внутри агрегата.

```csharp
// Application layer (Command Handler):
var order = await _repository.GetByIdAsync(id, ct);
order.Cancel(reason);           // изменение через агрегат
_repository.Delete(order);      // регистрация намерения
await _db.SaveChangesAsync(ct); // коммит через UoW
```

## Strongly Typed IDs

Каждый агрегат имеет свой тип ID — не `Guid` напрямую:

```csharp
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

Prevents confusing `OrderId` с `CustomerId` на уровне компилятора.

## Создание агрегатов — только через фабричные методы

`Entity<TId>.Id` имеет `protected set`, а конструкторы агрегатов — `protected`. Это сделано намеренно
(Always Valid Domain Model): нельзя создать невалидный агрегат снаружи и нельзя через
object-initializer (`new Order { Id = ... }`). Создавайте через статический фабричный метод, который
возвращает `Result<T>`:

```csharp
public sealed class Order : AggregateRoot<OrderId>
{
    private Order() { }                                  // для EF/ORM
    private Order(OrderId id, CustomerId customer) : base(id) { /* ... */ }

    public static Result<Order> Create(CustomerId customer) => /* валидация → */ new Order(OrderId.New(), customer);
}
```

- **EF Core:** параметрless `protected` ctor + backing fields материализуют агрегат при чтении — публичный сеттер `Id` не нужен.
- **Тесты:** строьте агрегаты тем же фабричным методом (или test-builder, вызывающим фабрику), а не object-initializer.

## Domain Services

Используются только когда логика не принадлежит ни одному агрегату:
```csharp
public interface IShippingCostCalculator : IDomainService
{
    Money Calculate(Address destination, Weight weight);
}
```

Реализация — в инфраструктуре или application layer.
