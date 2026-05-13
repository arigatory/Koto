# Koto.Testing

DDD-specific test helpers for Koto microservices.

## What's included

| Type | Purpose |
|---|---|
| `AggregateTestFixture<TAgg>` | Fluent Given/When/Then for aggregate unit tests |
| `AggregateAssertions<TAgg>` | Domain event assertions |
| `FakeRepository<TAgg, TId>` | In-memory `IRepository<TAgg,TId>` |
| `FakeIntegrationEventPublisher` | Captures published `IIntegrationEvent`s |
| `ResultAssertions<T>` | `.Should().BeSuccess()`, `.BeFailure()`, `.BeFailureWith("code")`, `.HaveValue(x)` |

## Aggregate unit tests

```csharp
public class OrderTests
{
    [Fact]
    public void Cancel_raises_OrderCancelled_event()
    {
        new AggregateTestFixture<Order>()
            .Given(new OrderPlaced(orderId, customerId))
            .When(o => o.Cancel("Customer request"))
            .Then()
            .ShouldHaveRaisedEvent<OrderCancelled>(e => e.Reason == "Customer request")
            .And.ShouldHaveRaisedExactly(1);
    }
}
```

## Result assertions

```csharp
var result = order.Cancel("reason");

result.Should().BeSuccess();
result.Should().BeFailureWith("orders.order.already-cancelled");
result.Should().HaveValue(expectedValue);
```

## Fake infrastructure

```csharp
var repo = new FakeRepository<Order, OrderId>();
var publisher = new FakeIntegrationEventPublisher();

// ... run use case ...

repo.All.Should().ContainSingle();
publisher.GetPublishedEvent<OrderPlacedEvent>().OrderId.Should().Be(orderId);
```

## IAggregateApply<TEvent> (optional)

Implement on your aggregate to enable state reconstruction in `Given()`:

```csharp
public class Order : AggregateRoot<OrderId>, IAggregateApply<OrderPlaced>
{
    public void Apply(OrderPlaced e) => Status = OrderStatus.Pending;
}
```
