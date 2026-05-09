# Koto.Messaging.Wolverine — Plan

**Phase:** 3 | **Status:** NOT STARTED
**Depends on:** Koto.Application + Wolverine + WolverineFx.Kafka

---

## Цель

Реализует `IIntegrationEventPublisher` и `IIntegrationCommandDispatcher` через Wolverine. Kafka transport для cross-service коммуникации. Consumer base classes с idempotency и DLQ.

## Checklist

### Publishing (outbound)

- [ ] `WolverineIntegrationEventPublisher` — implements `IIntegrationEventPublisher`:
  - `PublishAsync(IIntegrationEvent, ct)` → `_messageBus.PublishAsync(integrationEvent, ct)`
  - Wolverine маршрутизирует на Kafka topic на основе типа события

- [ ] `WolverineIntegrationCommandDispatcher` — implements `IIntegrationCommandDispatcher`:
  - Fire-and-forget: `SendAsync(IIntegrationCommand, ct)` → `_messageBus.SendAsync(command, ct)`
  - Request/reply: `SendAsync<TResult>(IIntegrationCommand<TResult>, ct)` → Wolverine request/reply с configurable timeout

- [ ] `DomainEventDispatchInterceptor` — `ISaveChangesInterceptor` для EF Core:
  - Перед `SaveChangesAsync`: собирает domain events из tracked агрегатов
  - Энкьюит события в Wolverine outbox (в рамках той же транзакции через `AddWolverineDbContext`)
  - После коммита: вызывает `ClearDomainEvents()` на агрегатах

### Consuming (inbound)

- [ ] `IntegrationEventConsumerBase<TEvent>` — base handler для Kafka consumers:
  - Проверяет idempotency (хранит processed `EventId` в БД, configurable window — по умолчанию 24h)
  - При дубликате: skip (логирует как info)
  - При unhandled exception: роутит в DLQ (dead letter queue)
  - Structured logging: `EventId`, `EventType`, `CorrelationId`, `ConsumerGroup`

- [ ] `IntegrationCommandConsumerBase<TCommand>` — аналогично для integration commands

- [ ] `KafkaSagaBase<TState>` — base для process managers:
  - Хранит `TState` в Marten (document store)
  - Transitions через методы с `[StartSaga]`, `[SagaHandler]` атрибутами Wolverine

### Middleware
- [ ] `CorrelationIdMiddleware` — Wolverine middleware: берёт `CorrelationId` из входящего сообщения, кладёт в `AsyncLocal` для propagation
- [ ] `IdempotencyMiddleware` — pluggable middleware: проверяет `EventId`/`MessageId` в таблице processed messages

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoWolverine(services, builder => { ... })`:
  - Регистрирует `WolverineIntegrationEventPublisher` как `IIntegrationEventPublisher`
  - Регистрирует `WolverineIntegrationCommandDispatcher` как `IIntegrationCommandDispatcher`
  - Подключает Kafka transport
  - Настраивает Wolverine outbox (требует EF Core DbContext)

## Kafka conventions

- Topic naming: `{service}.{event-type}` → `orders.order-placed`, `payments.payment-processed`
- Consumer group naming: `{consuming-service}.{event-type}-consumer`
- Wolverine Kafka limitation: `Requeue` error policy не работает корректно — использовать DLQ вместо этого

## Тесты
- [ ] IntegrationEventPublisher: публикует в Wolverine bus (mock)
- [ ] DomainEventDispatchInterceptor: события попадают в outbox при SaveChanges
- [ ] IntegrationEventConsumerBase: idempotency — дубликат не обрабатывается дважды
- [ ] IntegrationEventConsumerBase: DLQ routing при exception
- [ ] CorrelationId propagates через сообщения
