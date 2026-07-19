# Koto.Messaging.Wolverine

Wolverine + Kafka implementation of Koto messaging abstractions.

## What's included

| Type | Purpose |
|---|---|
| `WolverineIntegrationEventPublisher` | `IIntegrationEventPublisher` → `IMessageBus.PublishAsync` |
| `WolverineIntegrationCommandDispatcher` | `IIntegrationCommandDispatcher` → fire-and-forget + request/reply |
| `IntegrationEventConsumerBase<TEvent>` | Idempotent Kafka consumer base with DLQ routing |
| `IntegrationCommandConsumerBase<TCommand>` | Kafka command consumer base with structured logging |
| `CorrelationIdMiddleware` | Propagates Wolverine `Envelope.CorrelationId` via `CorrelationContext` |
| `IdempotencyMiddleware` | Policy-level deduplication (alternative to the consumer base check) |
| `IProcessedMessageStore` | Idempotency store abstraction (in-memory default; install [Koto.Messaging.Wolverine.Postgres](https://www.nuget.org/packages/Koto.Messaging.Wolverine.Postgres) for a durable PostgreSQL store in production) |

## Setup

```csharp
// Program.cs
builder.Services.AddKotoWolverine(opts =>
{
    opts.RequestReplyTimeout = TimeSpan.FromSeconds(10);
    opts.IdempotencyWindow = TimeSpan.FromHours(24);
});

builder.Host.UseWolverine(opts =>
{
    opts.UseKafka("localhost:9092").AutoProvisionTopics();

    // Route outbound events to topics
    opts.PublishMessage<OrderPlacedEvent>().ToKafkaTopic("orders.order-placed");

    // Listen for inbound events
    opts.ListenToKafkaTopic("payments.payment-processed").ProcessInline();

    // Propagate correlation IDs on every handler
    opts.Policies.AddMiddleware<CorrelationIdMiddleware>();

    // Auto-dispatch domain events from EF Core aggregates to the outbox
    opts.PublishDomainEventsFromEntityFrameworkCore<IHasDomainEvents, IDomainEvent>(
        e => e.DomainEvents);
});
```

## Kafka topic naming convention

- Event topics: `{service}.{event-type}` → `orders.order-placed`
- Consumer groups: `{consuming-service}.{event-type}-consumer`

## Implementing a consumer

```csharp
public sealed class PaymentProcessedConsumer
    : IntegrationEventConsumerBase<PaymentProcessedEvent>
{
    public PaymentProcessedConsumer(
        IProcessedMessageStore store,
        ILogger<IntegrationEventConsumerBase<PaymentProcessedEvent>> logger)
        : base(store, logger) { }

    protected override async Task ConsumeAsync(PaymentProcessedEvent @event, CancellationToken ct)
    {
        // domain logic here
    }
}
```

## Production idempotency store

Replace `InMemoryProcessedMessageStore` with a durable store:

```csharp
builder.Services.AddScoped<IProcessedMessageStore, PostgresProcessedMessageStore>();
```
