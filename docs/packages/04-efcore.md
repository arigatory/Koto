# Koto.Infrastructure.EFCore — Plan

**Phase:** 2 | **Status:** NOT STARTED
**Depends on:** Koto.Domain + Koto.Application + EF Core 10 + Wolverine (для outbox)

---

## Цель

EF Core интеграция с DDD паттернами: автоматический сбор domain events, outbox через Wolverine, базовые реализации репозиториев и value converters.

## Checklist

### Base DbContext
- [ ] `KotoDbContext` — abstract base DbContext:
  - Переопределяет `SaveChangesAsync`: перед коммитом собирает все `DomainEvents` из tracked агрегатов через change tracker, после коммита энкьюит в Wolverine outbox
  - Интеграция с `AddWolverineDbContext` (Wolverine EF Core outbox)
  - Опционально: `OnModelCreating` применяет `StronglyTypedIdConventions` и owned entity настройки по умолчанию

### Generic Repository
- [ ] `Repository<TAgg, TId>` — реализует `IRepository<TAgg, TId>`:
  - `GetByIdAsync` — `_context.Set<TAgg>().FindAsync(id, ct)`
  - `Add` — `_context.Set<TAgg>().Add(aggregate)`
  - `Delete` — `_context.Set<TAgg>().Remove(aggregate)`

### Value Converters
- [ ] `StronglyTypedIdValueConverter<TId, TRaw>` — EF Core ValueConverter base для strongly typed IDs
- [ ] `StronglyTypedIdConvention` — ModelBuilder convention: автоматически применяет converter для всех properties типа StronglyTypedId

### Specification Pattern
- [ ] `ISpecification<T>` — интерфейс: `Expression<Func<T, bool>> Criteria`, `List<Expression<Func<T, object>>> Includes`, `Expression<Func<T, object>>? OrderBy`
- [ ] `Specification<T>` — base class с builder методами: `AddCriteria`, `AddInclude`, `ApplyOrderBy`
- [ ] `SpecificationEvaluator` — `IQueryable<T> GetQuery(IQueryable<T> query, ISpecification<T> spec)` — применяет Where, Include, OrderBy

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoEFCore<TContext>(services, options)` — регистрирует DbContext с Wolverine outbox, регистрирует generic Repository

## Тесты (Koto.Infrastructure.EFCore.Tests)
- [ ] Repository CRUD через Testcontainers (PostgreSQL)
- [ ] Domain events собираются и очищаются после SaveChanges
- [ ] Outbox: events попадают в outbox таблицу в той же транзакции
- [ ] StronglyTypedId persists и reads корректно
- [ ] Specification: criteria, include, orderby применяются правильно
