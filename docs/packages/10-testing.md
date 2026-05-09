# Koto.Testing — Plan

**Phase:** 4 | **Status:** NOT STARTED
**Depends on:** Koto.Domain + Koto.Application + xUnit + Testcontainers.NET + AwesomeAssertions

---

## Цель

DDD-специфичные test helpers. Чистое unit-тестирование агрегатов без БД. Base классы для integration тестов с реальными контейнерами.

## Checklist

### Aggregate Unit Testing
- [ ] `AggregateTestFixture<TAgg>` — fluent тестирование агрегатов:
  ```csharp
  fixture
      .Given(new OrderItemAdded(productId, 2))
      .When(order => order.AddItem(anotherProductId, 1))
      .Then()
      .ShouldHaveRaisedEvent<OrderItemAdded>(e => e.ProductId == anotherProductId)
      .And.ShouldHaveRaisedExactly(2);
  ```
  - `Given(params IDomainEvent[] events)` — воссоздаёт агрегат из событий (для event-sourced) или через пустой конструктор + apply
  - `When(Action<TAgg> action)` — выполняет действие
  - `Then()` → `AggregateAssertions<TAgg>` — fluent assertions

- [ ] `AggregateAssertions<TAgg>`:
  - `ShouldHaveRaisedEvent<TEvent>(Func<TEvent, bool>? predicate = null)`
  - `ShouldNotHaveRaisedEvent<TEvent>()`
  - `ShouldHaveRaisedExactly(int count)`
  - `And` — цепочка assertions

### Fake Infrastructure
- [ ] `FakeRepository<TAgg, TId>` — in-memory `IRepository<TAgg, TId>`:
  - `Dictionary<TId, TAgg>` под капотом
  - Thread-safe для async тестов
  - Expose `All` property для assertions

- [ ] `FakeIntegrationEventPublisher` — реализует `IIntegrationEventPublisher`:
  - Накапливает опубликованные события в `List<IIntegrationEvent> PublishedEvents`
  - `T GetPublishedEvent<T>()` — возвращает первое событие типа T или throws

### Integration Test Base
- [ ] `IntegrationTestBase` — abstract xUnit base class с `IAsyncLifetime`:
  - Поднимает PostgreSQL Testcontainer (shared per test class через `IClassFixture`)
  - Опционально: Kafka Testcontainer
  - `WebApplicationFactory<TProgram>` с переопределёнными connection strings
  - `protected IServiceScope CreateScope()` — для resolving services в тестах
  - `protected Task ResetDatabaseAsync()` — очищает таблицы между тестами (Respawn или TRUNCATE)

### AwesomeAssertions Extensions
- [ ] `ResultAssertions<T>` — extensions для `Result<T>`:
  - `result.Should().BeSuccess()`
  - `result.Should().BeFailure()`
  - `result.Should().BeFailureWith("error.code")`
  - `result.Should().HaveValue(expectedValue)`

## Пример теста агрегата

```csharp
public class OrderTests
{
    [Fact]
    public void Cancel_WhenPending_RaisesCancelledEvent()
    {
        var fixture = new AggregateTestFixture<Order>();
        fixture
            .Given(new OrderPlaced(OrderId.New(), customerId))
            .When(o => o.Cancel("Customer request"))
            .Then()
            .ShouldHaveRaisedEvent<OrderCancelled>(e => e.Reason == "Customer request");
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ReturnsFailure()
    {
        var order = Order.Create(...).Value;
        order.Cancel("reason 1");

        var result = order.Cancel("reason 2");

        result.Should().BeFailureWith("orders.order.already-cancelled");
    }
}
```
