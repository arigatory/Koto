# Koto.Testing

DDD-specific test helpers for Koto microservices.

## What's included

| Type | Purpose |
|---|---|
| `AggregateTestFixture<TAgg>` | Fluent Given/When/Then for aggregate unit tests |
| `AggregateAssertions<TAgg>` | Domain event assertions |
| `FakeRepository<TAgg, TId>` | In-memory `IRepository<TAgg,TId>` |
| `FakeIntegrationEventPublisher` | Captures published `IIntegrationEvent`s |
| `ResultAssertions<T>` / `AppResultAssertions<T>` | `.Should().BeSuccess()`, `.BeFailure()`, `.BeFailureWith("code")`, `.HaveErrors("code1", "code2")`, `.HaveValue(x)`; application results also `.HaveErrorForField("email")` (case-insensitive: the wire name and `nameof(cmd.Email)` both match) |
| `ErrorCatalog` | `Scan(assemblies)` lists every error record with its code and detail keys; `ShouldBeValid(assemblies)` fails on malformed or duplicate codes (typed enrichments deriving from `EnrichedError` share their cause's code and are not duplicates) |

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
result.Should().HaveErrors("general.value.is-required", "general.value.invalid-length"); // all codes, in order
result.Should().HaveValue(expectedValue);

// Application results (handlers) carry enriched errors:
var appResult = await handler.HandleAsync(command);
appResult.Should().BeFailureWith("general.value.is-required").And.HaveErrorForField("email");
```

## Error catalog test

One test per service keeps the error contract honest — every record constructs, every code follows
`context.entity.problem`, no code is defined by two types:

```csharp
[Fact]
public void Error_catalog_is_valid() =>
    ErrorCatalog.ShouldBeValid(typeof(Order).Assembly, typeof(PlaceOrderHandler).Assembly);

// ErrorCatalog.Scan(...) returns (Type, Code, Message, DetailKeys) — the input for generating
// client-side types or documentation.
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
