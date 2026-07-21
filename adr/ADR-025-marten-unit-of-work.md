# ADR-025: MartenUnitOfWork — атомарные мульти-стрим коммиты

**Статус:** ✅ Принято · **Дата:** 2026-07-21

## Context

`IEventSourcedRepository.SaveAsync` коммитил один агрегат немедленно. Ledger-сценарии (двойная
запись: перевод = события в двух стримах Account + документ ProcessedOperation с идемпотентным
ключом) требуют атомарности нескольких стримов и документов в одной транзакции. Это блокер
фазы Wallet в RBG.

## Decision

- `IEventSourcedRepository.Append(agg)` — стейджит события в текущую scoped `IDocumentSession`
  без сохранения; `SaveAsync` = Append + commit (совместимость).
- `MartenAggregateTracker` (scoped) помнит застейдженные агрегаты; их `UncommittedEvents`
  очищаются только после успешного коммита.
- `MartenUnitOfWork : IUnitOfWork` (TryAddScoped в `AddKotoMarten`): Commit — `session.SaveChangesAsync`
  + очистка трекера; Rollback — `EjectAllPendingChanges` + сброс трекера. Совместим с
  `TransactionBehavior` (Begin — no-op: Marten-сессия транзакционна на SaveChanges).

## Consequences

- Паттерн Wallet: `repo.Append(from); repo.Append(to); session.Store(op); uow.CommitAsync()` —
  всё или ничего; идемпотентность через unique-документ в той же транзакции.
- `IUnitOfWork` теперь имеет реализации и для EF (ADR-022), и для Marten — TransactionBehavior
  работает в обоих мирах.
