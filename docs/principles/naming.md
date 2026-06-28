# Соглашения по именованию — Koto

## Пакеты и неймспейсы

```
Koto.Domain
Koto.Application
Koto.Validation
Koto.Infrastructure.EFCore
Koto.Infrastructure.Http
Koto.EventSourcing.Marten
Koto.Messaging.Wolverine
Koto.Api.FastEndpoints
Koto.Observability
Koto.Testing
```

## Классы домена

| Тип | Суффикс | Пример |
|---|---|---|
| Агрегат | нет | `Order`, `Customer` |
| Strongly Typed ID | `Id` | `OrderId`, `CustomerId` |
| Value Object | нет | `Email`, `Money`, `Address` |
| Domain Event | `DomainEvent` | `OrderPlacedDomainEvent` |
| Integration Event | `IntegrationEvent` | `OrderPlacedIntegrationEvent` |
| Integration Command | `IntegrationCommand` | `ShipOrderIntegrationCommand` |
| Domain Error class | `Errors` | `OrderErrors` (static class) |
| Repository interface | `IRepository` suffix or `I{Agg}Repository` | `IOrderRepository` |
| Domain Service interface | `I{Name}` | `IShippingCostCalculator` |

## CQRS

| Тип | Суффикс | Пример |
|---|---|---|
| Локальная команда | `Command` | `PlaceOrderCommand` |
| Локальный запрос | `Query` | `GetOrderQuery` |
| Command Handler | `Handler` | `PlaceOrderHandler` |
| Query Handler | `Handler` | `GetOrderHandler` |
| DTO (response) | `Dto` | `OrderDto`, `OrderSummaryDto` |

## Методы агрегата

- Поведение: глагол в повелительном наклонении — `Place()`, `Cancel()`, `AddItem()`, `Confirm()`.
- Фабричный метод: `Create(...)` возвращает `Result<TAgg>`.
- Применение события (event sourcing): `Apply(TEvent event)` — private или protected.

## События

Имена в **прошедшем времени**, агрегат + действие:
```
OrderPlaced, OrderCancelled, OrderItemAdded
PaymentProcessed, PaymentFailed
CustomerRegistered, CustomerEmailChanged
```

## Коды ошибок

```
general.value.is-required
general.value.invalid-length
general.entity.not-found
general.collection.too-small
general.collection.too-large
{service}.{entity}.{description}    ← orders.order.already-cancelled
```

## Файлы

- Один публичный тип на файл.
- Имя файла = имя класса/record/interface: `OrderId.cs`, `Email.cs`, `PlaceOrderCommand.cs`.
- Конфигурации EF Core: `{Entity}Configuration.cs` — `OrderConfiguration.cs`.
- Extension methods: `{Subject}Extensions.cs` — `ResultExtensions.cs`, `FluentValidationExtensions.cs`.
