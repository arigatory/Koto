# Koto.Domain — Plan

**Phase:** 1 | **Status:** NOT STARTED
**Depends on:** nothing (zero external dependencies)

Принципы: `docs/principles/ddd.md`, `docs/principles/errors-and-results.md`

---

## Цель

Чистые DDD building blocks. Фундамент всего остального. ~400 строк, полностью понятных и подконтрольных.

## Checklist

### Value Object
- [ ] `ValueObject` — abstract base с `GetEqualityComponents()`; `Equals`, `GetHashCode`, `==`, `!=`; ~30 строк

### Entity & Aggregate
- [ ] `Entity<TId>` — identity equality (два объекта равны если равны ID)
- [ ] `AggregateRoot<TId>` — extends Entity; private `List<IDomainEvent> _domainEvents`; методы `AddDomainEvent(IDomainEvent)`, `ClearDomainEvents()`, `IReadOnlyList<IDomainEvent> DomainEvents`

### Strongly Typed ID
- [ ] `StronglyTypedId<T>` — readonly record struct; wraps Guid/int/string; IComparable; `ToString()` override

### Domain Events
- [ ] `IDomainEvent` — интерфейс: `Guid EventId { get; }`, `DateTime OccurredAt { get; }`
- [ ] `DomainEvent` — base record: `Guid EventId = Guid.NewGuid()`, `DateTime OccurredAt = DateTime.UtcNow`

### Error & Result
- [ ] `Error` — `sealed record(string Code, string Message)`; `Serialize()` → `"code::message"`
- [ ] `Errors.General` — static class: `ValueIsRequired()`, `InvalidLength(min,max)`, `NotFound(field,id?)`, `CollectionIsTooSmall(min,actual)`, `CollectionIsTooLarge(max,actual)`
- [ ] `Result<T>` — sealed class:
  - `IsSuccess`, `IsFailure`, `Value` (throws if failure), `Error` (throws if success)
  - Static: `Result<T>.Success(T)`, `Result<T>.Failure(Error)`
  - Implicit conversions: `T → Result<T>`, `Error → Result<T>`
  - `Map<TNew>(Func<T, TNew>)`
  - `Bind<TNew>(Func<T, Result<TNew>>)`
  - `Tap(Action<T>)` → возвращает себя
  - `TapError(Action<Error>)` → возвращает себя
  - `Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)`
  - `Ensure(Func<T, bool> predicate, Error error)`
  - Async overloads: `MapAsync`, `BindAsync`, `TapAsync`, `EnsureAsync`

### Repository
- [ ] `IRepository<TAgg, TId>` where TAgg : AggregateRoot<TId>:
  - `Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default)`
  - `void Add(TAgg aggregate)`
  - `void Delete(TAgg aggregate)`

### Markers
- [ ] `IDomainService` — пустой marker interface

## Что НЕ делать
- Никаких зависимостей на NuGet-пакеты
- Никакого `Maybe<T>` — nullable C# достаточно
- Никакого `ErrorType` enum — смысл в коде ошибки
- Не добавлять `SaveAsync` в IRepository

## Тесты (Koto.Domain.Tests)
- [ ] ValueObject equality (одинаковые компоненты = равны, разные = не равны)
- [ ] Entity identity equality
- [ ] AggregateRoot: AddDomainEvent, ClearDomainEvents
- [ ] Result: Success/Failure, Map, Bind, Tap, Match, Ensure, implicit conversions
- [ ] Result async overloads
- [ ] Error.Serialize() format
