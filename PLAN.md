# Koto — Plan Index

Детальные планы пакетов: `docs/packages/`
Принципы: `docs/principles/`

---

## История релизов

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
