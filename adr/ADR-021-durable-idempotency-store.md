# ADR-021: Durable idempotency store — отдельный пакет Koto.Messaging.Wolverine.Postgres

**Статус:** ✅ Принято · **Дата:** 2026-07-20

## Context

`IntegrationEventConsumerBase<T>` дедуплицирует по `IProcessedMessageStore`, но в поставке была только in-memory реализация — при рестарте сервиса окно идемпотентности обнуляется, и at-least-once доставка Kafka приводит к повторной обработке. Строка `// services.AddScoped<IProcessedMessageStore, PostgresProcessedMessageStore>()` в `AddKotoWolverine` была TODO без реализации. Первый реальный потребитель, которому это блокирует прод — Real Board Games (все консюмеры между сервисами Identity/Events/Wallet).

## Options considered

1. **Npgsql-реализация внутри `Koto.Messaging.Wolverine`** — минус: тянет Npgsql всем потребителям messaging, включая тех, кто не на Postgres; нарушает принцип «маленькие пакеты, бери что нужно».
2. **Реализация в `Koto.Infrastructure.EFCore`** — минус: неверный слой (идемпотентность — концерн messaging, не персистентности агрегатов); тянет EF в консюмеры.
3. **Отдельный пакет `Koto.Messaging.Wolverine.Postgres`** (raw Npgsql, без EF) — плюс: opt-in зависимость, симметрия с экосистемой Wolverine (`WolverineFx.Postgresql`), место для будущих PG-специфичных фич messaging.

## Decision

Вариант 3. Дополнительно в базовом пакете `AddKotoWolverine` регистрация in-memory стора переведена с `AddSingleton` на `TryAddSingleton`, чтобы порядок вызова `AddPostgresProcessedMessageStore` / `AddKotoWolverine` не влиял на результат (durable-регистрация выигрывает всегда: Replace + TryAdd).

## Consequences

- 13-й пакет в наборе: релизится тем же пайплайном (pack всего slnx), отдельного сопровождения почти не требует.
- Семантика остаётся at-least-once: `MarkAsProcessedAsync` выполняется после `ConsumeAsync` вне общей транзакции — окно дубля при падении между ними. Это осознанно: строгая дедупликация бизнес-операций делается на уровне приложения (детерминированный OperationId + unique constraint). Документировано в README пакета.
- Redis/другие сторы — по той же схеме отдельными пакетами, если появится потребность.
