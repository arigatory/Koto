# Sample: OrderFlow

**Паттерны:** Saga Orchestration, Saga Choreography (сравнение), Transactional Outbox, Idempotent Consumer, ACL + HTTP

---

## Что демонстрирует

Полный цикл оформления заказа через три микросервиса. Реализован **дважды** — через Orchestration и Choreography — чтобы показать разницу.

## Сервисы

```
OrderService      ← агрегат Order, CQRS, saga orchestrator (в варианте orchestration)
PaymentService    ← принимает IntegrationCommand, списывает деньги
InventoryService  ← резервирует товар
ShippingService   ← планирует отгрузку
```

## Вариант A: Orchestration

`OrderSaga` в OrderService управляет всем сценарием:

```
POST /orders → PlaceOrderCommand
  → OrderSaga.Start()
    → send ChargePaymentIntegrationCommand → PaymentService
    → on PaymentProcessed → send ReserveStockIntegrationCommand → InventoryService
    → on StockReserved → send ScheduleShipmentIntegrationCommand → ShippingService
    → on ShipmentScheduled → Order.Complete() → saga ends

  Compensation on failure:
    → PaymentFailed → Order.Cancel() → saga ends
    → StockReservationFailed → send RefundPaymentIntegrationCommand → saga ends
```

**Koto использует:** `KafkaSagaBase<OrderSagaState>`, `IIntegrationCommandDispatcher`, `IntegrationCommandConsumerBase`

## Вариант B: Choreography

Каждый сервис реагирует на событие предыдущего самостоятельно:

```
OrderPlacedIntegrationEvent →
  PaymentService: PaymentProcessedIntegrationEvent | PaymentFailedIntegrationEvent →
    InventoryService: StockReservedIntegrationEvent | StockReservationFailedIntegrationEvent →
      ShippingService: ShipmentScheduledIntegrationEvent

Compensation:
  PaymentFailed → (OrderService listens) → Order.Cancel()
  StockReservationFailed → (PaymentService listens) → RefundPaymentIntegrationCommand
```

**Koto использует:** `IIntegrationEventPublisher`, `IntegrationEventConsumerBase` с idempotency

## Что показывает сравнение

| | Orchestration | Choreography |
|---|---|---|
| Где логика потока | в одном месте (Saga) | размазана по сервисам |
| Отслеживание состояния | легко (SagaState) | сложно |
| Coupling | saga знает все сервисы | сервисы знают только события |
| Отладка | просто | сложнее |

## Структура репозитория

```
samples/OrderFlow/
  src/
    OrderService/         ← Clean Architecture
    PaymentService/       ← Vertical Slice
    InventoryService/
    ShippingService/
  infra/
    docker-compose.yml    ← все сервисы + Kafka + PostgreSQL
    k8s/                  ← манифесты для K8s deployment + HPA
  tests/
    OrderFlow.IntegrationTests/   ← E2E тест через все сервисы
```
