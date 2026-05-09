# Архитектура — Соглашения Koto

## Зависимости между пакетами

```
Koto.Domain
  └── Koto.Application
        ├── Koto.Validation
        ├── Koto.Infrastructure.EFCore
        ├── Koto.Infrastructure.Http
        ├── Koto.EventSourcing.Marten
        ├── Koto.Messaging.Wolverine
        └── Koto.Api.FastEndpoints

Koto.Observability  (независимый, подключается к хосту)
Koto.Testing        (зависит от Domain + Application, только для тестов)
```

Правило: **зависимости идут только вверх**. `Koto.Domain` не знает ни о чём ниже.

## Clean Architecture

```
YourService/
  YourService.Domain/
    Aggregates/
      Order.cs
      OrderId.cs
    ValueObjects/
      Money.cs
    Events/
      OrderPlacedDomainEvent.cs
    Errors/
      OrderErrors.cs
    Interfaces/
      IOrderRepository.cs

  YourService.Application/
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
      OrderPlacedDomainEventHandler.cs   ← in-process, конвертирует в IntegrationEvent
    IntegrationEvents/
      OrderPlacedIntegrationEvent.cs

  YourService.Infrastructure/
    Persistence/
      OrderRepository.cs
      AppDbContext.cs
      Configurations/
        OrderConfiguration.cs
    HttpClients/
      PaymentServiceHttpClient.cs
    Messaging/
      (Wolverine конфигурация, consumer bases)

  YourService.Api/
    Endpoints/
      Orders/
        PlaceOrderEndpoint.cs
        GetOrderEndpoint.cs
```

## Vertical Slice Architecture

```
YourService/
  Features/
    PlaceOrder/
      PlaceOrderCommand.cs
      PlaceOrderHandler.cs
      PlaceOrderEndpoint.cs
      PlaceOrderValidator.cs
      OrderPlacedDomainEvent.cs
      OrderPlacedIntegrationEvent.cs
    GetOrder/
      GetOrderQuery.cs
      GetOrderHandler.cs
      GetOrderEndpoint.cs
      OrderDto.cs
    CancelOrder/
      ...
  Domain/
    Order.cs              ← агрегат (shared между фичами)
    OrderId.cs
    OrderErrors.cs
  Infrastructure/
    AppDbContext.cs
    OrderRepository.cs
```

**Выбор:** Vertical Slice лучше для больших команд и независимых фич. Clean Architecture лучше для сложного домена с богатой логикой. Koto building blocks работают одинаково в обоих случаях.

## Правила зависимостей в сервисе

- **Domain** — не знает об Application, Infrastructure, API. Чистый C#.
- **Application** — знает только о Domain. Никаких `using EntityFrameworkCore`, `using Kafka` и т.д.
- **Infrastructure** — реализует интерфейсы из Domain и Application. Знает об EF Core, Wolverine, HttpClient.
- **API** — знает об Application (диспатчит команды/запросы). Не знает об Infrastructure напрямую.

## Поток данных

```
HTTP Request
  → FastEndpoints Endpoint
    → ICqrsDispatcher.SendAsync(command)
      → CommandHandler (Application)
        → IRepository.GetByIdAsync (Domain interface)
          → EF Core Repository (Infrastructure impl)
        → aggregate.DoSomething()
          → AddDomainEvent(new SomethingHappenedEvent())
        → IRepository.Add(aggregate)
        → DbContext.SaveChangesAsync()
          → Wolverine Outbox: {data + event} → one commit
  ← Result<T, Error>
  ← HTTP Response (200 / ProblemDetails)

Wolverine (async, from outbox):
  → SomethingHappenedHandler
    → IIntegrationEventPublisher.PublishAsync(new SomethingHappenedIntegrationEvent())
      → Kafka → other microservices
```
