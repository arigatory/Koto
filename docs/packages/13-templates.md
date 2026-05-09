# Koto.Templates — Plan

**Phase:** 5 | **Status:** NOT STARTED
**NuGet package:** `Koto.Templates` (установка: `dotnet new install Koto.Templates`)

---

## Цель

`dotnet new` шаблоны для быстрого старта. После `dotnet new install Koto.Templates` разработчик получает готовый, компилируемый, протестированный стартовый проект без boilerplate.

---

## Шаблон 1: `koto-microservice`

Полный микросервис с выбором архитектурного стиля.

```bash
dotnet new koto-microservice \
  --name Orders \
  --arch clean          # clean | slice
  --transport kafka     # kafka | rabbitmq | none
  --eventsourcing       # включить Marten Event Sourcing
  --output ./src/OrderService
```

### Что генерирует (`--arch clean`):

```
OrderService/
  OrderService.Domain/
    Aggregates/
      Order.cs              ← пример агрегата с domain events
      OrderId.cs
    ValueObjects/
      Money.cs
    Events/
      OrderPlacedDomainEvent.cs
    Errors/
      OrderErrors.cs
    Interfaces/
      IOrderRepository.cs

  OrderService.Application/
    Commands/
      PlaceOrder/
        PlaceOrderCommand.cs
        PlaceOrderHandler.cs
        PlaceOrderValidator.cs
    Queries/
      GetOrder/
        GetOrderQuery.cs
        GetOrderHandler.cs
        OrderDto.cs
    EventHandlers/
      OrderPlacedDomainEventHandler.cs
    IntegrationEvents/
      OrderPlacedIntegrationEvent.cs

  OrderService.Infrastructure/
    Persistence/
      AppDbContext.cs
      OrderRepository.cs
      Configurations/
        OrderConfiguration.cs
    Migrations/                    ← первая миграция EF Core
    DependencyInjection.cs

  OrderService.Api/
    Endpoints/
      Orders/
        PlaceOrderEndpoint.cs
        GetOrderEndpoint.cs
    Program.cs                     ← минимальный, всё через AddKoto*()

  OrderService.Tests/
    Domain/
      OrderTests.cs                ← пример unit теста с AggregateTestFixture
    Application/
      PlaceOrderHandlerTests.cs    ← пример с FakeRepository
    Architecture/
      ArchitectureFitnessTests.cs  ← пример fitness functions
    Integration/
      OrderEndpointTests.cs        ← пример IntegrationTestBase

  docker-compose.yml               ← PostgreSQL + Kafka для local dev
  .env.example
```

### Что генерирует (`--arch slice`):

```
OrderService/
  Features/
    PlaceOrder/
      PlaceOrderCommand.cs
      PlaceOrderHandler.cs
      PlaceOrderEndpoint.cs
      PlaceOrderValidator.cs
      OrderPlacedDomainEvent.cs
      OrderPlacedIntegrationEvent.cs
    GetOrder/
      ...
  Domain/
    Order.cs
    OrderId.cs
    OrderErrors.cs
  Infrastructure/
    AppDbContext.cs
    ...
  Program.cs
  Tests/
    ...
```

---

## Шаблон 2: `koto-domain`

Только доменный проект — для добавления в существующее решение.

```bash
dotnet new koto-domain --name Payments --output ./src/Payments.Domain
```

Генерирует: базовый агрегат, value object, strongly typed ID, errors class.

---

## Шаблон 3: `koto-consumer`

Kafka consumer сервис — минимальный сервис, который только потребляет события.

```bash
dotnet new koto-consumer \
  --name NotificationConsumer \
  --event OrderPlacedIntegrationEvent \
  --output ./src/NotificationService
```

Генерирует: `Program.cs` с Wolverine + Kafka, consumer handler, idempotency setup, health checks, docker-compose.

---

## Техническая реализация

- Шаблоны упакованы как `Koto.Templates` NuGet package
- Используют стандартный механизм `dotnet new` (template.json)
- Параметры через `--arch`, `--transport`, `--eventsourcing` флаги
- Все generated файлы компилируются и тесты проходят из коробки

## Checklist

- [ ] `koto-microservice --arch clean` — компилируется, тесты зелёные
- [ ] `koto-microservice --arch slice` — компилируется, тесты зелёные
- [ ] `koto-microservice --eventsourcing` — подключает Marten вместо EF Core репозитория
- [ ] `koto-microservice --transport kafka` — подключает Wolverine Kafka transport
- [ ] `koto-domain` — минимальный domain проект
- [ ] `koto-consumer` — работающий Kafka consumer
- [ ] Все шаблоны: `docker-compose.yml` для local dev (PostgreSQL + Kafka)
- [ ] README в каждом сгенерированном проекте: как запустить, как добавить свой агрегат
- [ ] `Koto.Templates` публикуется на NuGet.org
