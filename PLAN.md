# Koto — Plan Index

Детальные планы пакетов: `docs/packages/`
Принципы: `docs/principles/`

---

## Фазы

### Phase 1 — Domain Core
**Status: DONE**
| Пакет | План | Статус |
|---|---|---|
| `Koto.Domain` | [docs/packages/01-domain.md](docs/packages/01-domain.md) | DONE |
| `Koto.Application` | [docs/packages/02-application.md](docs/packages/02-application.md) | DONE |
| `Koto.Validation` | [docs/packages/03-validation.md](docs/packages/03-validation.md) | DONE |

### Phase 2 — Infrastructure
**Status: DONE**
| Пакет | План | Статус |
|---|---|---|
| `Koto.Infrastructure.EFCore` | [docs/packages/04-efcore.md](docs/packages/04-efcore.md) | DONE |
| `Koto.Infrastructure.Http` | [docs/packages/05-http.md](docs/packages/05-http.md) | DONE |
| `Koto.EventSourcing.Marten` | [docs/packages/06-marten.md](docs/packages/06-marten.md) | DONE |

### Phase 3 — Messaging + API
**Status: DONE**
| Пакет | План | Статус |
|---|---|---|
| `Koto.Messaging.Wolverine` | [docs/packages/07-wolverine.md](docs/packages/07-wolverine.md) | DONE |
| `Koto.Api.FastEndpoints` | [docs/packages/08-fastendpoints.md](docs/packages/08-fastendpoints.md) | DONE |

### Phase 4 — Observability + Testing
**Status: DONE**
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
| `koto-microservice` | полный микросервис | NOT STARTED |
| `koto-domain` | только доменный проект | NOT STARTED |
| `koto-consumer` | Kafka consumer сервис | NOT STARTED |

Подробнее: [docs/packages/13-templates.md](docs/packages/13-templates.md)

### Phase 6 — Samples
**Status: NOT STARTED**
| Sample | Паттерны | Статус |
|---|---|---|
| `OrderFlow` | Saga Orchestration + Choreography + Outbox | NOT STARTED |
| `StreamProcessor` | Kafka stream processing, stateful consumers | NOT STARTED |
| `ApiGateway` | YARP + JWT auth + API Composition | NOT STARTED |
| `DataPipeline` | Batch processing + Koto.Scheduling + K8s CronJob | NOT STARTED |
| `RealTimeBoard` | Event Sourcing + CQRS + SignalR + GraphQL | NOT STARTED |

Подробнее: [docs/samples/](docs/samples/)

### Phase 7 — Infrastructure + Guides
**Status: NOT STARTED**
| Артефакт | Описание | Статус |
|---|---|---|
| `infra/k8s/` | HPA, Ingress, Helm chart шаблон | NOT STARTED |
| `infra/observability/` | Prometheus + Grafana + Loki + Tempo | NOT STARTED |
| `infra/ci-cd/` | GitHub Actions + Dockerfile | NOT STARTED |
| `docs/guides/monolith-decomposition.md` | Strangler Fig + Koto при переходе | NOT STARTED |
| `docs/guides/team-topologies.md` | bounded contexts → команды | NOT STARTED |
| `docs/guides/fitness-functions.md` | architectural tests с Koto.Testing.Architecture | NOT STARTED |
| `docs/guides/k8s-scaling.md` | HPA + KEDA + Kafka consumer lag | NOT STARTED |

---

## ⚠️ Перед стартом реализации — исправить ADR

- [ ] Унифицировать сигнатуру: **`Result<T, Error>`** (два параметра) во всех ADR (001, 002, 004, 005, 009)
- [ ] Заменить `UseSqlServer` на `UseNpgsql` в ADR-006 и ADR-013
- [ ] Исправить факт о лицензии FluentValidation v8 в ADR-009 (Apache 2.0, не Xceed)
- [ ] Убрать `Koto.Contracts` из ADR-010, зафиксировать `Koto.Application`
- [ ] Согласовать `IIntegrationEvent` (интерфейс) vs `IntegrationEvent` (abstract record) в ADR-010/011

## Старт следующей сессии
```bash
dotnet new sln -n Koto
dotnet new classlib -n Koto.Domain -o src/Koto.Domain --framework net10.0
```
Читать: [docs/packages/01-domain.md](docs/packages/01-domain.md)
