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
| Domain Error | record, имя — условие в прошедшем/состоянии | `OrderAlreadyCancelled`, `OtpExpired` (файл `Errors/OrderErrors.cs`, namespace `*.Domain.Errors`) |
| Application Error (данные из базы) | record : `Error` или `: EnrichedError(Cause)` | `EmailTaken`, `MeetupFullWithAlternative` |
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

Не меньше трёх kebab-case сегментов через точку (валидируется при создании `Error`):

```
general.value.is-required
general.value.invalid-length
general.entity.not-found
general.collection.too-small
general.collection.too-large
validation.rule.failed              ← правило FluentValidation без доменной фабрики
general.server.internal-error       ← необработанное исключение (500)
{service}.{entity}.{description}    ← orders.order.already-cancelled
```

Суффиксы несут HTTP-класс: `*.not-found` → 404, `*.already-*` / `*.conflict` → 409, `*.unauthorized` → 401, `*.forbidden` → 403.

## Поля `details`

camelCase, скаляры и массивы скаляров; идентификаторы именуются по сущности (`orderId`, не `id`), чтобы ключи не конфликтовали при нескольких ошибках. Ключи — имена свойств record (`RetryAfterSeconds` → `retryAfterSeconds`).

## Файлы

- Один публичный тип на файл.
- Имя файла = имя класса/record/interface: `OrderId.cs`, `Email.cs`, `PlaceOrderCommand.cs`.
- Конфигурации EF Core: `{Entity}Configuration.cs` — `OrderConfiguration.cs`.
- Extension methods: `{Subject}Extensions.cs` — `ResultExtensions.cs`, `FluentValidationExtensions.cs`.
