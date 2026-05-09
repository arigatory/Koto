# Koto Samples

Каждый sample — рабочее .NET приложение, которое запускается через `docker-compose up`.

| Sample | Паттерны | Сложность |
|---|---|---|
| [OrderFlow](orderflow.md) | Saga Orchestration, Saga Choreography, Transactional Outbox | ★★★ |
| [StreamProcessor](stream-processor.md) | Kafka stream processing, stateful consumers, windowed aggregation | ★★★ |
| [ApiGateway](api-gateway.md) | YARP reverse proxy, JWT auth, API Composition | ★★ |
| [DataPipeline](data-pipeline.md) | Batch processing, Koto.Scheduling, K8s CronJob | ★★ |
| [RealTimeBoard](realtime-board.md) | Event Sourcing, CQRS, SignalR (WebSockets), GraphQL | ★★★ |

## Как запустить любой sample

```bash
cd samples/OrderFlow
docker-compose up -d        # PostgreSQL + Kafka + Prometheus + Grafana
dotnet run --project src/Api
```

Grafana dashboard: http://localhost:3000 (admin/admin)
