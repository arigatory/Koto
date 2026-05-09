# Sample: StreamProcessor

**Паттерны:** Kafka stream processing, stateful consumers, windowed aggregation, choreography

---

## Что демонстрирует

Потоковая обработка событий: агрегация метрик в реальном времени, обнаружение паттернов, фильтрация и трансформация потоков.

## Сценарий

Система аналитики продаж — обрабатывает поток `OrderPlacedIntegrationEvent` и производит:
- скользящие агрегаты (сумма продаж за последние 5 минут)
- алерты при аномалиях (резкий рост/падение заказов)
- enriched события (добавляет данные о клиенте из кэша)

## Что показывает

```
Kafka topic: orders.order-placed
  → FilterProcessor: фильтрует тестовые заказы
  → EnrichProcessor: добавляет CustomerTier из Redis cache
  → AggregationProcessor: windowed sum(amount) per region, tumbling 5min window
  → AlertProcessor: если avg drop > 30% → publish AnomalyDetectedIntegrationEvent
  → Kafka topic: analytics.sales-aggregated
  → Kafka topic: analytics.anomalies
```

## Koto использует

- `IntegrationEventConsumerBase<T>` — base для каждого processor
- `IIntegrationEventPublisher` — публикует обработанные события downstream
- Stateful processing через Marten document store (хранит оконные агрегаты)
- `Koto.Observability` — метрики lag, throughput, processing time

## Структура

```
samples/StreamProcessor/
  src/
    Analytics.Processors/
      FilterProcessor.cs
      EnrichProcessor.cs
      AggregationProcessor.cs    ← stateful, windowed
      AlertProcessor.cs
    Analytics.Api/               ← REST API для чтения агрегатов
  infra/
    docker-compose.yml           ← Kafka + Redis + PostgreSQL + Grafana
    k8s/
      hpa.yaml                   ← масштабирование по Kafka consumer lag (KEDA)
```

## K8s scaling note

HPA по CPU не подходит для Kafka consumers — нагрузка не отражается в CPU.
Правильно: **KEDA** (Kubernetes Event-Driven Autoscaling) масштабирует по consumer lag:
```yaml
triggers:
  - type: kafka
    metadata:
      topic: orders.order-placed
      lagThreshold: "100"    # один pod на каждые 100 unprocessed messages
```
