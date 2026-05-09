# Koto.EventSourcing.Marten — Plan

**Phase:** 2 | **Status:** NOT STARTED
**Depends on:** Koto.Domain + Marten (MIT, PostgreSQL)

---

## Цель

Event Sourcing поверх Marten. Агрегат восстанавливается из потока событий. Может использоваться совместно с EF Core в одном сервисе (Marten = event streams, EF Core = read models).

## Checklist

### Event Sourced Aggregate Base
- [ ] `EventSourcedAggregateRoot<TId>` — base class:
  - `private List<IDomainEvent> _uncommittedEvents`
  - `protected void RaiseEvent(IDomainEvent @event)` — применяет событие через `Apply` + добавляет в uncommitted
  - `protected abstract void Apply(IDomainEvent @event)` — реализует потребитель
  - `IReadOnlyList<IDomainEvent> UncommittedEvents`
  - `void ClearUncommittedEvents()`
  - `static TAgg Reconstitute(IEnumerable<IDomainEvent> events)` — factory через Apply

### Repository
- [ ] `IEventSourcedRepository<TAgg, TId>` where TAgg : EventSourcedAggregateRoot<TId>:
  - `Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct)`
  - `Task SaveAsync(TAgg aggregate, CancellationToken ct)` — здесь SaveAsync оправдан: Marten не использует UoW/DbContext, каждое AppendAsync — атомарная операция
- [ ] `MartenEventSourcedRepository<TAgg, TId>` — реализация:
  - `GetByIdAsync` — `session.Events.AggregateStreamAsync<TAgg>(id, ct)`
  - `SaveAsync` — `session.Events.Append(id, uncommittedEvents); await session.SaveChangesAsync(ct)`

### Projections
- [ ] `AsyncProjection<TReadModel>` — base для async проекций в Marten Async Daemon
- [ ] `InlineProjection<TReadModel>` — base для inline проекций

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoMarten(services, connectionString, configure?)` — настраивает Marten store, регистрирует `IEventSourcedRepository<,>`

## Совместная работа с EF Core

Marten и EF Core можно использовать в одном сервисе на одной PostgreSQL БД:
- Marten: управляет event streams (`mt_events`, `mt_streams` таблицы)
- EF Core: управляет read models и другими сущностями

Это поддерживаемый и задокументированный паттерн в Marten.

## Тесты (Koto.EventSourcing.Marten.Tests)
- [ ] Save + GetById через Testcontainers (PostgreSQL)
- [ ] Reconstitute: агрегат восстанавливается из событий
- [ ] Версионирование: optimistic concurrency (append fails if stream modified concurrently)
- [ ] AsyncProjection обновляет read model при добавлении события
