# Koto — .NET DDD/Microservices Library Suite

Open-source набор NuGet-пакетов для DDD, CQRS, Event Sourcing и Kafka в .NET микросервисах.
Референс-потребитель: **agent-server** (`/Users/ivan/source/sk-agent-demo/koto-agent/apps/agent-server`) — реальный wiring всех слоёв (Domain/Application/Validation/EFCore/FastEndpoints/Observability) через PackageReference.

## Текущий статус
- Все **14 пакетов** опубликованы на nuget.org (профиль `arigatory`); стабильная `0.4.0`, в работе `0.5.0-preview.1`
  (новая модель ошибок и конверт ответа — ADR-026, `docs/packages/16-error-model-envelope.md`).
- Релиз: bump `<Version>` в `src/Directory.Build.props` → тег `vX.Y.Z` → push → CI `publish.yml` пакует (версия из тега) и пушит все пакеты на nuget.org
  через Trusted Publishing (OIDC + `NuGet/login`, политика на nuget.org для `arigatory/Koto` + `publish.yml`, глоб `Koto.*`); API-ключей с истекающим сроком нет.
- Breaking changes: руководство для потребителей `docs/migrations/<from>-to-<to>.md` (англ., код до/после), на него ссылаются
  README затронутых пакетов («Upgrading from …») и `PackageReleaseNotes` в `src/Directory.Build.props`; `PLAN.md` — русская история релизов.
- Шаблоны (`Koto.Templates`, Phase 5) — НЕ начаты; новые сервисы скаффолдятся вручную.
- Детальный индекс фаз: `PLAN.md`
- Планы пакетов: `docs/packages/`
- Скилл для агентов-потребителей: `skills/koto/` (источник правды, публикуется с репо; `~/.claude/skills/koto` у автора — симлинк на него).
  Меняется вместе с публичным API и README: конвенции, чеклист миграции, `koto-version` во frontmatter.

## Стек (все MIT/Apache 2.0)
- Messaging: **Wolverine** + WolverineFx.Kafka
- Event Sourcing: **Marten** (PostgreSQL)
- ORM: **EF Core 10**
- API: **FastEndpoints** + **Koto.Api.AspNetCore** (`AppResult<T>` → `Envelope<T>`: Minimal API, MVC, FastEndpoints)
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
  `AddKotoApplication(o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(), asm)` + `AddKotoValidation(asm)`.
  Порядок регистрации = порядок исполнения (Logging → Validation → Transaction).
  Behaviors регистрируются как open generic и закрываются по **конкретному** типу команды/запроса.
  `AddKotoApplication` падает при старте, если у команды/запроса нет хендлера (или их несколько).

### Result, Error, AppResult, Envelope (v0.5, ADR-026)
- Три слоя: домен возвращает `Result<T>` (ошибки `Error`), хендлеры — `AppResult<T>` (ошибки `EnrichedError`),
  API отдаёт `Envelope<T>` `{ success, result, errors[{code,message,field,details}], correlationId, timestamp }`.
  Неявной конверсии `Result<T>` → `AppResult<T>` нет: `return x.Errors.ForField("email")` / `.Enrich()` / `.Enrich(map)`.
- `Error` **абстрактный**: каждая ошибка — record из каталога (`sealed record OtpExpired(DateTimeOffset At) : Error("identity.otp.expired", "…")`),
  свойства record = гарантированные `details`. Код ≥3 kebab-case сегментов, валидируется при создании.
  Общие: `new Errors.General.NotFound("order", id)`, `ValueIsRequired()`, `InvalidLength(min, max)`.
- Проброс всех ошибок: `return x.Errors;` (`ErrorList` неявно → результат). Первая ошибка — `FirstError`
  (заголовок, статус), пробрасывать её нельзя.
- `Result.Combine(...)`, `Result.Sequence/Traverse` собирают все ошибки; `Result.Success()/Failure()` для void.
- HTTP-статус — грубый класс (`*.not-found` 404, `*.already-*` 409, `general.*`/с полем 400, иначе 422), при нескольких
  ошибках решает первая; `KotoApiOptions.Map(...)` для исключений. Исключения — только для неожиданного (500).
- Нет `Maybe<T>` — используем C# nullable (`T?`, `??`, `?.`). Нет `ErrorType` enum — смысл несёт код.

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
