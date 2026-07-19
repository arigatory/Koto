# Koto — .NET DDD/Microservices Library Suite

Open-source набор NuGet-пакетов для DDD, CQRS, Event Sourcing и Kafka в .NET микросервисах.
Референс-потребитель: **agent-server** (`/Users/ivan/source/sk-agent-demo/koto-agent/apps/agent-server`) — реальный wiring всех слоёв (Domain/Application/Validation/EFCore/FastEndpoints/Observability) через PackageReference.

## Текущий статус
- Все **12 пакетов** собраны и опубликованы на nuget.org (профиль `arigatory`), версия `0.3.0-preview.x`.
- Релиз: bump `<Version>` в `src/Directory.Build.props` → тег `vX.Y.Z` → push → CI `publish.yml` пакует (версия из тега) и пушит все пакеты на nuget.org.
- Шаблоны (`Koto.Templates`, Phase 5) — НЕ начаты; новые сервисы скаффолдятся вручную.
- Детальный индекс фаз: `PLAN.md`
- Планы пакетов: `docs/packages/`

## Стек (все MIT/Apache 2.0)
- Messaging: **Wolverine** + WolverineFx.Kafka
- Event Sourcing: **Marten** (PostgreSQL)
- ORM: **EF Core 10**
- API: **FastEndpoints** + **Koto.Api.AspNetCore** (транспорт-независимый Result→HTTP: Minimal API, MVC)
- Observability: Serilog + OpenTelemetry .NET
- Validation: FluentValidation **v12** (диапазон `[12.0.0,13.0.0)`, Apache 2.0)
- Testing: xUnit + NSubstitute + AwesomeAssertions + Testcontainers.NET

## Принципы (подробнее в docs/principles/)

### DDD
- Агрегат — единственная точка изменения состояния. Никаких прямых изменений через репозиторий.
- `AggregateRoot<TId>` хранит uncommitted domain events; диспатч через Wolverine outbox.
- Value Object = `record` (простые) или `ValueObject` abstract base (кастомная equality).
- Валидация живёт в фабричных методах: `Email.Create(string) → Result<Email>`.
- Репозиторий: `Add`/`Delete` — синхронные (change tracker), `GetByIdAsync` — async. Коммит = зона UoW.
- Интерфейс `IRepository` живёт в **Koto.Application** (не в Domain), рядом с `IUnitOfWork`:
  порт принадлежит слою потребителя (хендлерам); домен — только сущности/VO/события/Result.
- Pipeline behaviors — opt-in через `KotoApplicationOptions`:
  `AddKotoApplication(o => o.AddLoggingBehavior().AddTransactionBehavior(), asm)`.
  Порядок регистрации = порядок исполнения; рекомендуемый: Logging → Validation → Transaction.
  Behaviors регистрируются как open generic и закрываются по **конкретному** типу команды/запроса.

### Result и Error
- `Result<T>` — собственная реализация (вдохновлена Khorikovым), без внешних зависимостей.
- Multi-error: `Result<T>.Errors` несёт все ошибки (`Failure(IEnumerable<Error>)`), `Error` — первая.
- Статический компаньон `Result`: `Success()` / `Failure(...)` для void-потоков (`Result<Unit>`)
  и `Result.Combine(...)` — агрегация нескольких результатов (все ошибки, не первая).
- `Error` = `record(string Code, string Message)` + опциональный `Field` (имя поля для
  validation problem details; заполняет application-слой). Код: `"general.value.is-required"`, `"orders.order.not-found"`.
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
- FluentAssertions v8+ → коммерческий (Xceed) → заменён на **AwesomeAssertions**
- EventStoreDB v24.10+ → ESLv2, использовать **Marten**

> ⚠️ **FluentValidation — НЕ ловушка.** Apache 2.0 на всех версиях (включая v12).
> Используется v12 в диапазоне `[12.0.0,13.0.0)` (см. ревизию ADR-009). Не путать с
> **FluentAssertions**, которая действительно ушла в коммерцию в v8.
