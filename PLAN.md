# Koto — Plan Index

Детальные планы пакетов: `docs/packages/`
Принципы: `docs/principles/`

---

## История релизов

- **v0.4.0** — **первый стабильный релиз** (сведение обкатки на Real Board Games, RBG фазы 0–8):
  все 14 пакетов выходят без суффикса preview; API заморожен по состоянию preview.15, breaking
  изменений нет. EF Core выровнен до 10.0.10. Установка теперь без `--prerelease`.
  Полный список закрытых за проект пробелов: durable ProcessedMessageStore (p5), фикс потери
  доменных событий вне Wolverine-хендлеров / `EfCoreUnitOfWork` (p6), пагинация `PagedList<T>` (p7),
  perfection pass — `INonTransactionalCommand`, sealed `HandleAsync`, DX-фиксы (p8), s2s-auth для
  HTTP ACL (p9), Marten async daemon opt-in (p10), фикс `AddServiceHttpClient` (p11), обязательная
  consumer group (p12), schema-прозрачная шина (p13), дефолтная retry-политика консюмеров (p14),
  `Koto.Testing.Integration` + `IntegrationEventTopics` (p15).

- **v0.3.0-preview.15** — новый пакет **`Koto.Testing.Integration`** (правило трёх из тестов RBG):
  `HeaderTestAuthHandler` (+`AddHeaderTestAuthentication`/`WithTestUser`), `Eventually.AssertAsync`,
  `RawJsonKafkaProducer`. Резолв топика вынесен в `Koto.Application.IntegrationEventTopics` —
  одна конвенция для публикации, подписки и тестов.

- **v0.3.0-preview.14** — **дефолтная retry-политика консюмеров в `UseKotoKafka`**: без неё Wolverine
  уводил сообщение в dead letter после первого исключения — паттерн «throw для ретрая при гонке
  топиков» молча терял события. Теперь: inline-повторы 200мс/1с/3с → отложенные 10с/30с/60с
  (durable при inbox) → dead letter; специфичные политики сервиса имеют приоритет.

- **v0.3.0-preview.13** — **schema-прозрачная шина**: `PublishIntegrationEvents` публикует чистый JSON
  (web-casing, без Wolverine-заголовков) — топики читаются любым инструментом/стеком; новый
  `ListenToIntegrationEvent<T>()` — подписка по конвенции (`T.Topic` + `ReceiveRawJson<T>` + inline).
  Без этого сообщения не-Wolverine продюсеров (интеграционные тесты, другие стеки) молча
  не маппились в типы.
- **v0.3.0-preview.12** — **breaking:** `UseKotoKafka` требует явную consumer group
  (`UseKotoKafka(bootstrap, consumerGroup, assemblies)`): без неё два сервиса могли оказаться
  в одной Kafka-группе с разными подписками — партиции не назначаются, события молча теряются
  (найдено двухсервисным сага-тестом RBG). Также ставится `AutoOffsetReset.Earliest`.
- **v0.3.0-preview.11** — **фикс: `AddServiceHttpClient` был сломан из коробки** — регистрировал
  `CorrelationIdHandler`, зависящий от `ICorrelationIdAccessor`, реализацию которого никто не
  регистрировал (контейнер падал на валидации). Добавлен `NullCorrelationIdAccessor` (TryAdd);
  приложение может подменить своим мостом к API-слою.
- **v0.3.0-preview.10** — `AddKotoMarten(..., asyncDaemon: DaemonMode.Solo)` — опциональный запуск
  Marten async-демона (нужен async-проекциям вроде statement-историй); раньше требовался прямой
  доступ к `AddMarten()`-expression, который Koto скрывает.
- **v0.3.0-preview.9** — s2s-аутентификация для HTTP ACL (закрывает последний запланированный
  пробел RBG фазы 3): клиент — `AddServiceHttpClient(..., o => o.ApiKey = key)` добавляет
  `X-Service-Key` к каждому запросу (`Koto.Infrastructure.Http`); сервер — схема `ServiceKey`
  (`AddAuthentication().AddServiceKey(key)`, `Koto.Api.AspNetCore`), constant-time сравнение,
  principal с ролью `Service` → `Roles("Service")` на внутренних эндпоинтах.
- **v0.3.0-preview.8** — «perfection pass» по итогам RBG фаз 0–2, четыре DX-улучшения + один enabler:
  1. **Авто-конвертация StronglyTypedId** (ADR-023): `KotoDbContext` сам регистрирует pre-convention
     конвертеры для всех id-типов из сборок сущностей — ручные `Properties<XId>().HaveConversion<…>()`
     в контекстах потребителей больше не нужны (включая не-ключевые ссылки и ключи owned-типов).
  2. **`INonTransactionalCommand`** (маркер в Koto.Application): команды с security-счётчиками
     (OTP-попытки) исключаются из `TransactionBehavior` и управляют коммитами явно.
  3. **Конвенционный bootstrap Wolverine** (ADR-024): `UseKotoKafka(bootstrap, assemblies)` +
     `PublishIntegrationEvents(contractAssemblies)` (роутинг по `public const string Topic`,
     fail-fast при отсутствии) в базовом пакете; `UseKotoDurableOutbox(pg)` в
     Koto.Messaging.Wolverine.Postgres (тянет WolverineFx.Postgresql/EntityFrameworkCore).
  4. **`CommandEndpoint`/`QueryEndpoint` реализуют `HandleAsync` sealed** — сабклассы содержат только
     `Configure()`. **Breaking:** свои переопределения `HandleAsync` в наследниках удалить
     (Mapped-варианты уже работали так).
  5. **Marten multi-stream UoW** (ADR-025): `IEventSourcedRepository.Append` + `MartenUnitOfWork`
     (TryAddScoped IUnitOfWork в `AddKotoMarten`) — атомарный коммит нескольких стримов и документов
     одной транзакцией; enabler ledger-сценариев (Wallet RBG). **Breaking:**
     `MartenEventSourcedRepository` получил второй ctor-параметр `MartenAggregateTracker`.
- **v0.3.0-preview.7** — пагинация: `PagedList<T>` в `Koto.Application` (Items/Page/PageSize/TotalCount,
  TotalPages/HasNext/HasPrevious, `Map`, `Empty`) + `IQueryable<T>.ToPagedListAsync(page, pageSize)`
  в `Koto.Infrastructure.EFCore` (COUNT + Skip/Take; требует упорядоченного запроса).
  Возвращайте из query-хендлеров: `IQuery<PagedList<OrderDto>>`.
- **v0.3.0-preview.6** — **фикс потери доменных событий вне Wolverine-хендлеров** (ADR-022):
  `PublishDomainEventsFromEntityFrameworkCore` — codegen-политика, работающая только внутри хендлеров;
  события из обычных HTTP-флоу молча терялись. Новый `EfCoreUnitOfWork<TContext>` в
  `Koto.Infrastructure.EFCore` — дефолтная реализация `IUnitOfWork` (регистрируется в
  `AddKotoEFCore` через TryAddScoped): `CommitAsync` публикует uncommitted domain events через
  `IDbContextOutbox` в одной транзакции с изменениями. E2E-тест `DomainEventOutboxFlowTests`.
  **Docs-фиксы:** README EFCore — durable outbox требует `PersistMessagesWithPostgresql` +
  `UseDurableOutboxOnAllSendingEndpoints` + `Discovery.IncludeAssembly` (раньше опускалось);
  README Wolverine — `AutoProvisionTopics()` → `AutoProvision()`, идемпотентность → ссылка на
  `AddPostgresProcessedMessageStore`.
- **v0.3.0-preview.5** — новый пакет **`Koto.Messaging.Wolverine.Postgres`** (ADR-021): durable
  PostgreSQL-реализация `IProcessedMessageStore` (`AddPostgresProcessedMessageStore(connString, opts)`) —
  дедупликация консюмеров переживает рестарт; авто-создание схемы (`koto.processed_messages`, opt-out),
  фоновая очистка записей старше `IdempotencyWindow`, валидация идентификаторов схемы/таблицы,
  `DeleteExpiredAsync` в публичном API. **Не-breaking изменение базового пакета:** регистрация
  `InMemoryProcessedMessageStore` в `AddKotoWolverine` теперь `TryAddSingleton` (было `AddSingleton`) —
  durable-стор выигрывает независимо от порядка вызовов. Спека: docs/packages/15-wolverine-postgres.md.
- **v0.3.0-preview.4** — docs-only релиз, API не менялся: корневой README (таблица 12 пакетов
  со ссылками на nuget, реальный ручной scaffold вместо несуществующего `Koto.Templates`,
  секция Documentation), фикс примера в `Koto.EventSourcing.Marten/README.md`
  (`Handle` → `HandleAsync`, `ICommandHandler<TCommand, TResult>` без `Result<>` в type arg),
  актуализация CLAUDE.md (12 пакетов на nuget, референс-потребитель agent-server).
- **v0.3.0-preview.1** — критический фикс пайплайна + multi-error Result + FluentValidation 12 +
  новый пакет `Koto.Api.AspNetCore`. **Breaking changes / migration notes для потребителей (IceFlow, Task137):**
  - **Behaviors резолвятся по конкретному типу команды/запроса** (`IPipelineBehavior<CreateUserCommand, Result<T>>`),
    а не по маркеру. Валидаторы `AbstractValidator<КонкретнаяКоманда>` теперь находятся пайплайном
    (раньше — молча игнорировались). Behaviors, реализованные против маркеров
    (`IPipelineBehavior<ICommand<T>, …>`), больше не вызываются — перевести на open generic
    регистрацию или конкретный тип.
  - **Дефолтные behaviors — opt-in:** `AddKotoApplication(o => o.AddLoggingBehavior().AddTransactionBehavior(), asm)`.
    Порядок регистрации = порядок исполнения (рекомендуется Logging → Validation → Transaction).
  - **`Result<T>` multi-error:** `Errors` (все ошибки), `Failure(IEnumerable<Error>)`, null-guards
    в `Success`/`Failure`; статический компаньон `Result.Success()`/`Result.Failure()`/`Result.Combine(...)`;
    `MatchAsync`/`TapErrors`; `IResultBase.Errors`; `IResultFactory<TSelf>` (static abstract `FromErrors`).
  - **`Error`:** добавлен `Field` (имя поля для validation problem details); **удалён `Serialize()`**
    (транспортный хак FV7 — заменён `ValidationFailure.CustomState`).
  - **События:** `IDomainEvent`/`IIntegrationEvent` — `OccurredAt` теперь `DateTimeOffset`,
    `EventId`/`OccurredAt`/`CorrelationId` стали `init` → переживают JSON round-trip
    (дедупликация у консюмеров работает).
  - **`IRepository` переехал:** `Koto.Domain` → `Koto.Application` (поменять `using`; см. ревизию ADR-005).
  - **FluentValidation 7.\* → `[12.0.0,13.0.0)`:** cast-хаки убраны; `MustBeValueObject<T,TSource,TVO>`
    generic по типу источника (не только `string`); доменный `Error` едет через `CustomState` + `ErrorCode`;
    `ValidationBehavior` использует `ValidateAsync` (async-правила работают) и возвращает
    N структурных ошибок вместо склеенной строки.
  - **`Koto.Api.FastEndpoints`:** `KotoProblemDetails` переехал в новый пакет (namespace
    `Koto.Api.AspNetCore`); `StatusCodeFrom` заменён на расширяемый `KotoHttpErrorOptions`;
    **незамапленные коды теперь 422 (было 500)**; `AddKotoApi(configureErrors)`; эндпоинты
    отдают все ошибки `Result.Errors`.
  - **Новый пакет `Koto.Api.AspNetCore`** (ADR-020): `ToHttpResult` (Minimal API) /
    `ToActionResult` (MVC), RFC 7807 multi-error problem details, registry код → статус.
  - **Прочее:** `ConfigureAwait(false)` во всех библиотеках + CA2007 = error (src);
    `Entity.IsTransient` (transient-сущности не равны); `StronglyTypedId.CompareTo` бросает
    при сравнении разных типов id; `IQueryBase`; guard от `ReflectionTypeLoadException`
    при сканировании сборок; `Koto.Testing`: ассерт `HaveErrors(params string[])`.
- **v0.2.0-preview.1** — `Koto.Api.FastEndpoints`: `MappedCommandEndpoint`/`MappedQueryEndpoint`
  (request DTO ≠ command, server-derived поля вне контракта) + `ClaimsPrincipal.GetUserId()`.
  `Koto.Application`: `TransactionBehavior` теперь делает rollback при `Result.Failure`
  (маркер `Koto.Domain.IResultBase`). `AggregateRoot.DomainEvents` помечен `[NotMapped]`.
- **v0.1.0-preview.x** — Phase 1–4 (см. ниже).

## Фазы

### Phase 1 — Domain Core
**Status: DONE** | Опубликовано: v0.1.0-preview.3+
| Пакет | План | Статус |
|---|---|---|
| `Koto.Domain` | [docs/packages/01-domain.md](docs/packages/01-domain.md) | DONE |
| `Koto.Application` | [docs/packages/02-application.md](docs/packages/02-application.md) | DONE |
| `Koto.Validation` | [docs/packages/03-validation.md](docs/packages/03-validation.md) | DONE |

### Phase 2 — Infrastructure
**Status: DONE** | Опубликовано: v0.1.0-preview.3+
| Пакет | План | Статус |
|---|---|---|
| `Koto.Infrastructure.EFCore` | [docs/packages/04-efcore.md](docs/packages/04-efcore.md) | DONE |
| `Koto.Infrastructure.Http` | [docs/packages/05-http.md](docs/packages/05-http.md) | DONE |
| `Koto.EventSourcing.Marten` | [docs/packages/06-marten.md](docs/packages/06-marten.md) | DONE |

### Phase 3 — Messaging + API
**Status: DONE** | Опубликовано: v0.1.0-preview.4+
| Пакет | План | Статус |
|---|---|---|
| `Koto.Messaging.Wolverine` | [docs/packages/07-wolverine.md](docs/packages/07-wolverine.md) | DONE |
| `Koto.Api.FastEndpoints` | [docs/packages/08-fastendpoints.md](docs/packages/08-fastendpoints.md) | DONE |
| `Koto.Api.AspNetCore` | [docs/packages/14-api-aspnetcore.md](docs/packages/14-api-aspnetcore.md) | DONE (v0.3.0-preview.1) |

### Phase 4 — Observability + Testing
**Status: DONE** | Опубликовано: v0.1.0-preview.5
| Пакет | План | Статус |
|---|---|---|
| `Koto.Observability` | [docs/packages/09-observability.md](docs/packages/09-observability.md) | DONE |
| `Koto.Testing` | [docs/packages/10-testing.md](docs/packages/10-testing.md) | DONE |
| `Koto.Scheduling` | [docs/packages/11-scheduling.md](docs/packages/11-scheduling.md) | DONE |
| `Koto.Testing.Architecture` | [docs/packages/12-testing-architecture.md](docs/packages/12-testing-architecture.md) | OPTIONAL — пропустить |

### Phase 5 — Templates
**Status: NOT STARTED**
| Артефакт | Описание | Статус |
|---|---|---|
| `Koto.Templates` NuGet | dotnet new шаблоны | NOT STARTED |
| `koto-microservice` | полный микросервис (FastEndpoints + EFCore + Wolverine + OTel) | NOT STARTED |
| `koto-domain` | только доменный проект | NOT STARTED |
| `koto-consumer` | Kafka consumer сервис | NOT STARTED |

Подробнее: [docs/packages/13-templates.md](docs/packages/13-templates.md)

### Phase 6 — Samples
**Status: NOT STARTED** | Приоритет: низкий — делать по мере необходимости
| Sample | Паттерны | Статус |
|---|---|---|
| `OrderFlow` | Saga Orchestration + Choreography + Outbox | NOT STARTED |
| `StreamProcessor` | Kafka stream processing, stateful consumers | NOT STARTED |
| `ApiGateway` | YARP + JWT auth + API Composition | NOT STARTED |
| `DataPipeline` | Batch processing + Koto.Scheduling + K8s CronJob | NOT STARTED |
| `RealTimeBoard` | Event Sourcing + CQRS + SignalR + GraphQL | NOT STARTED |

Подробнее: [docs/samples/](docs/samples/)

### Phase 7 — Infrastructure + Guides
**Status: NOT STARTED** | Приоритет: низкий
| Артефакт | Описание | Статус |
|---|---|---|
| `infra/k8s/` | HPA, Ingress, Helm chart шаблон | NOT STARTED |
| `infra/observability/` | Prometheus + Grafana + Loki + Tempo | NOT STARTED |
| `docs/guides/monolith-decomposition.md` | Strangler Fig + Koto при переходе | NOT STARTED |
| `docs/guides/team-topologies.md` | bounded contexts → команды | NOT STARTED |
| `docs/guides/k8s-scaling.md` | HPA + KEDA + Kafka consumer lag | NOT STARTED |
| `docs/guides/fitness-functions.md` | architectural tests — только после Koto.Testing.Architecture | BLOCKED |

---

## ADR — статус

Долг по ADR закрыт (2026-06-29). Итог проверки исходного списка против кода:

- [x] Унифицировать сигнатуру `Result<T>` (один параметр, не `Result<T, Error>`) в ADR-001/002/004/005/009/010 + CLAUDE.md. Оставлены `Result<T, E>` только там, где описывается отвергнутая альтернатива (CSharpFunctionalExtensions).
- [x] Лицензия FluentValidation: ADR-009 был **верен** (Apache 2.0 на всех версиях). Ошибка была в CLAUDE.md и memory (путаница с FluentAssertions) — исправлено.
- [x] Написаны недостающие ADR: **ADR-017** (Koto.Observability), **ADR-018** (Koto.Testing), **ADR-019** (Koto.Infrastructure.Http).
- [x] ~~`UseSqlServer` → `UseNpgsql`~~ — уже было корректно (`UseSqlServer` нигде нет).
- [x] ~~Убрать `Koto.Contracts` из ADR-010~~ — упоминаний `Koto.Contracts` нет; уже `Koto.Application`.
- [x] ~~`IIntegrationEvent` vs `IntegrationEvent`~~ — уже согласовано (интерфейс + abstract record), соответствует коду.
- ℹ️ ADR для Wolverine/FastEndpoints/Scheduling уже существуют (006/008/014) — в исходном списке были перечислены ошибочно.

**Опциональный остаток (не входил в этот проход):** мелкий дрейф API в ADR-014 (`KotoJob` → `ScheduledJobBase`/`BatchJobBase`, `UsePersistentStore` → `UseJobStore`) и ADR-008 (не упоминает `MappedCommandEndpoint`/`MappedQueryEndpoint` из v0.2). Освежить при следующем касании этих пакетов.

**Обновление 2026-07-04 (v0.3.0-preview.1):** ADR-002/003/005/009 получили ревизии (multi-error Result + `IResultFactory`; `Error.Field` − `Serialize()`; перенос `IRepository` в Application; апгрейд FluentValidation до v12). Добавлен **ADR-020** (Koto.Api.AspNetCore — транспорт-независимый Result→HTTP маппинг, fallback 422).
