# Sample: RealTimeBoard

**Паттерны:** Event Sourcing, CQRS, SignalR (WebSockets), GraphQL, REST — интеграция фронтенда через разные протоколы

---

## Что демонстрирует

Real-time дашборд заказов: изменения видны мгновенно у всех подключённых клиентов. Три способа получить данные — REST, GraphQL, WebSocket — один и тот же backend.

## Архитектура

```
OrderService (Event Sourcing via Marten)
  │
  ├── REST API (FastEndpoints)
  │     GET /orders/{id}          ← read model projection
  │     POST /orders              ← command
  │
  ├── GraphQL API (Hot Chocolate)
  │     query { orders { id status total } }
  │     subscription { orderUpdated { id status } }   ← real-time через WebSocket
  │
  └── SignalR Hub
        /hubs/orders              ← push обновления всем клиентам при событии

Next.js Frontend
  ├── REST: обычные страницы (SSR)
  ├── GraphQL: сложные запросы с вложенными данными
  └── WebSocket: live обновления без polling
```

## Event Sourcing + CQRS поток

```
POST /orders → PlaceOrderCommand
  → Order aggregate (EventSourcedAggregateRoot)
    → RaiseEvent(OrderPlacedDomainEvent)
  → MartenEventSourcedRepository.SaveAsync()
    → события в Marten event stream
  → Marten Async Daemon (projection runner)
    → OrderSummaryProjection обновляет read model
    → Wolverine publishes OrderUpdatedIntegrationEvent
  → SignalR Hub: await Clients.All.SendAsync("OrderUpdated", dto)
  → GraphQL subscription: pushed to subscribers
```

## Read models (проекции)

```csharp
// Inline projection — обновляется синхронно при append события
public class OrderSummaryProjection : SingleStreamProjection<OrderSummary>
{
    public void Apply(OrderPlacedDomainEvent e, OrderSummary summary) { ... }
    public void Apply(OrderCancelledDomainEvent e, OrderSummary summary) { ... }
}

// Async projection — через Marten Async Daemon, eventually consistent
public class OrdersByRegionProjection : EventProjection
{
    public void Project(OrderPlacedDomainEvent e, IDocumentOperations ops) { ... }
}
```

## GraphQL vs REST vs WebSocket — когда что

| Протокол | Лучше для |
|---|---|
| REST | простые CRUD, кэширование, публичные API |
| GraphQL | сложные запросы, mobile (экономия трафика), BFF |
| WebSocket | real-time: чаты, live dashboards, collaborative editing |
| Kafka (queues) | асинхронная интеграция между сервисами |

## Koto использует

- `Koto.EventSourcing.Marten` — агрегат + проекции
- `Koto.Api.FastEndpoints` — REST endpoints
- `Koto.Observability` — distributed tracing через все протоколы

## Структура

```
samples/RealTimeBoard/
  src/
    OrderService.Api/            ← REST + GraphQL + SignalR в одном хосте
      Endpoints/                 ← FastEndpoints (REST)
      GraphQL/                   ← Hot Chocolate schema
      Hubs/
        OrdersHub.cs             ← SignalR
    OrderService.Domain/
    OrderService.Infrastructure/
    Frontend/                    ← Next.js приложение
      pages/
      hooks/
        useOrderUpdates.ts       ← WebSocket hook
  infra/
    docker-compose.yml           ← сервис + PostgreSQL + Redis (SignalR backplane)
    k8s/
      deployment.yaml
      hpa.yaml                   ← HPA по CPU (SignalR = CPU-bound)
      redis-deployment.yaml      ← SignalR backplane для multi-pod
```

## Важно для K8s multi-pod

SignalR при нескольких инстансах требует backplane (Redis) — иначе клиент подключён к pod A, а событие приходит в pod B. Демонстрирует реальную production-проблему и её решение.
