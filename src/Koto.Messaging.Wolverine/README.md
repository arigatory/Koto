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
    opts.UseKafka("localhost:9092").AutoProvision();

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

Replace `InMemoryProcessedMessageStore` with the durable PostgreSQL store from
[Koto.Messaging.Wolverine.Postgres](https://www.nuget.org/packages/Koto.Messaging.Wolverine.Postgres):

```csharp
builder.Services.AddPostgresProcessedMessageStore(connectionString);
```

## Convention bootstrap

```csharp
builder.Host.UseWolverine(opts =>
{
    // transport + AutoProvision + explicit consumer group + correlation + discovery + retry policy
    opts.UseKotoKafka(kafkaConnectionString, "my-service", typeof(SomeHandler).Assembly)
        .PublishIntegrationEvents(typeof(OrderPlacedV1).Assembly);          // route by `public const string Topic`
    // + opts.UseKotoDurableOutbox(pgConnectionString) из Koto.Messaging.Wolverine.Postgres
});
```

Каждый `IIntegrationEvent` контрактной сборки обязан объявлять `public const string Topic = "service.event-name";` — тип без константы валит старт (fail fast вместо молчаливо потерянных событий).

## Default consumer retry policy

`UseKotoKafka` устанавливает дефолтную политику ошибок консюмеров (без неё Wolverine уводит
сообщение в dead letter после первого же исключения):

1. inline-повторы с паузами 200мс → 1с → 3с — гасят короткие гонки топиков
   («событие-предпосылка ещё не обработано» — штатная ситуация при подписке на несколько топиков;
   консюмер в этом случае просто бросает исключение);
2. отложенные повторы через 10с → 30с → 60с (при durable inbox переживают рестарт);
3. только затем — dead letter queue.

Более специфичные политики сервиса (`opts.Policies.OnException<MyException>()...`) имеют приоритет
над этим дефолтом. Паттерн консюмера: если обязательные предпосылки события ещё не готовы —
бросайте исключение, чтобы сработал повтор; молчаливый `return` теряет данные навсегда.
