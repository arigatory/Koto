# Koto — .NET DDD/Microservices Library Suite

Open-source набор NuGet-пакетов для DDD, CQRS, Event Sourcing и Kafka в .NET микросервисах.
Первый потребитель: **Task137** (`/Users/ivan/source/task137`).

## Текущий статус
- **Phase 1** — Domain Core: NOT STARTED
- Детальный индекс фаз: `PLAN.md`
- Планы пакетов: `docs/packages/`

## Стек (все MIT/Apache 2.0)
- Messaging: **Wolverine** + WolverineFx.Kafka
- Event Sourcing: **Marten** (PostgreSQL)
- ORM: **EF Core 10**
- API: **FastEndpoints**
- Observability: Serilog + OpenTelemetry .NET
- Validation: FluentValidation **v7** (pinned — v8+ коммерческий)
- Testing: xUnit + NSubstitute + AwesomeAssertions + Testcontainers.NET

## Принципы (подробнее в docs/principles/)

### DDD
- Агрегат — единственная точка изменения состояния. Никаких прямых изменений через репозиторий.
- `AggregateRoot<TId>` хранит uncommitted domain events; диспатч через Wolverine outbox.
- Value Object = `record` (простые) или `ValueObject` abstract base (кастомная equality).
- Валидация живёт в фабричных методах: `Email.Create(string) → Result<Email, Error>`.
- Репозиторий: `Add`/`Delete` — синхронные (change tracker), `GetByIdAsync` — async. Коммит = зона UoW.

### Result и Error
- `Result<T>` — собственная реализация (вдохновлена Khorikovым), без внешних зависимостей.
- `Error` = `record(string Code, string Message)`. Код: `"general.value.is-required"`, `"orders.order.not-found"`.
- Нет `Maybe<T>` — используем C# nullable (`T?`, `??`, `?.`).
- Нет `ErrorType` enum — смысл несёт код.

### События
- `IDomainEvent` — внутренний, меняется свободно, никогда не выходит за пределы сервиса.
- `IIntegrationEvent` — внешний контракт, требует версионирования, публикуется в Kafka.
- `IIntegrationCommand` — команда другому сервису (fire-and-forget через Kafka).
- `IIntegrationCommand<TResult>` — команда с ответом (HTTP или Kafka request/reply).
- Правило: префикс `Integration` = пересекает границу сервиса.

### Архитектура
- Поддерживаются Clean Architecture и Vertical Slice Architecture.
- `Koto.*` пакеты не диктуют структуру папок — только building blocks.
- `Koto.Application` — только интерфейсы, без зависимостей на инфраструктуру.

### Лицензионные ловушки (не использовать)
- MediatR v13+ → коммерческий (v12 = MIT, можно пинить)
- MassTransit v9+ → коммерческий (v8 = Apache 2.0)
- FluentValidation v8+ → коммерческий (v7 = Apache 2.0)
- FluentAssertions v8+ → заменён на **AwesomeAssertions**
- EventStoreDB v24.10+ → ESLv2, использовать **Marten**
