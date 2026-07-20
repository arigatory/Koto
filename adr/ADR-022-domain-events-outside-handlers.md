# ADR-022: Доменные события из кода вне Wolverine-хендлеров — EfCoreUnitOfWork

**Статус:** ✅ Принято · **Дата:** 2026-07-20

## Context

README и docs обещали флоу `aggregate.AddDomainEvent → SaveChangesAsync → outbox → in-process handler`,
опираясь на `opts.PublishDomainEventsFromEntityFrameworkCore<IHasDomainEvents, IDomainEvent>(...)`.
Обнаружено на первом реальном потребителе (RBG Identity, e2e-тест с Kafka): эта настройка — codegen-политика,
работающая **только внутри Wolverine-хендлеров**. При сохранении из обычного HTTP-эндпоинта
(FastEndpoints → `ICommandHandler` → `SaveChangesAsync`) доменные события молча терялись.
Дополнительно: у `IUnitOfWork` (Koto.Application) не было ни одной реализации в пакетах —
каждый потребитель писал свою.

## Options considered

1. **SaveChangesInterceptor**, публикующий события — минус: интерцептору нужен scoped `IDbContextOutbox`,
   а DbContext может жить дольше scope; порядок «конверты в той же транзакции» через интерцептор хрупок.
2. **Требовать Wolverine HTTP-эндпоинты** — минус: ломает выбор FastEndpoints (ADR-008).
3. **`EfCoreUnitOfWork<TContext>` в Koto.Infrastructure.EFCore**: на `CommitAsync` собирает
   `DomainEvents` из ChangeTracker и публикует через `IDbContextOutbox` (`Enroll` →
   `PublishAsync` → `SaveChangesAndFlushMessagesAsync`) — конверты и данные в одной транзакции.
   Заодно закрывает отсутствие дефолтного `IUnitOfWork`.

## Decision

Вариант 3. Регистрация в `AddKotoEFCore<TContext>` через `TryAddScoped` — своя реализация потребителя
всегда выигрывает. Без Wolverine в DI (нет `IDbContextOutbox`) — деградация до обычного `SaveChangesAsync`.
С явной транзакцией (`BeginTransactionAsync`) конверты пишутся тем же SaveChanges, отправка —
`FlushOutgoingMessagesAsync` после коммита.

Требования окружения задокументированы в README: durable outbox нуждается в
`opts.PersistMessagesWithPostgresql(conn)` + `opts.Policies.UseDurableOutboxOnAllSendingEndpoints()`
(раньше README это опускал) и `opts.Discovery.IncludeAssembly(...)` для хендлеров вне entry assembly.

## Consequences

- Обещание README теперь выполняется и проверено e2e-тестом (`DomainEventOutboxFlowTests`:
  Testcontainers PG, durable storage, in-process handler).
- `PublishDomainEventsFromEntityFrameworkCore` остаётся полезной для сохранений внутри
  Wolverine-хендлеров (консюмеры) — механизмы дополняют друг друга.
- Потребители, уже написавшие свой `IUnitOfWork`, не затронуты (TryAdd).
