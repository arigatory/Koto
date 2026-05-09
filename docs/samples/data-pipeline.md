# Sample: DataPipeline

**Паттерны:** Batch processing, scheduled jobs, distributed locking, K8s CronJob, pipeline из нескольких сервисов

---

## Что демонстрирует

Конвейер пакетной обработки: несколько сервисов обрабатывают данные по расписанию. При нескольких инстансах (HPA) каждый job выполняется ровно один раз.

## Сценарий

Ежедневный отчёт о продажах:
1. `DataCollectorService` — собирает данные из OrderService за прошедший день
2. `DataTransformerService` — нормализует и обогащает данные
3. `ReportGeneratorService` — генерирует PDF-отчёт
4. `NotificationService` — рассылает отчёт по email

## Архитектура pipeline

```
08:00 UTC (Quartz / K8s CronJob trigger)
  │
  ▼
DataCollectorService
  → BatchJobBase<OrderRecord>: fetches page by page from OrderService
  → публикует OrderDataCollectedIntegrationEvent (с chunk ID)

OrderDataCollectedIntegrationEvent →
  DataTransformerService
    → трансформирует chunk
    → публикует OrderDataTransformedIntegrationEvent

OrderDataTransformedIntegrationEvent →
  ReportGeneratorService
    → когда все chunks получены → генерирует отчёт
    → публикует ReportGeneratedIntegrationEvent

ReportGeneratedIntegrationEvent →
  NotificationService → отправляет email
```

## Два подхода к запуску по расписанию

**A) Koto.Scheduling (внутри сервиса):**
```csharp
builder.Services.AddKotoScheduling(q =>
{
    q.AddJob<DataCollectionJob>(cron: "0 0 8 * * ?"); // Quartz clustered
});
```
Плюс: distributed lock из коробки, мониторинг через Koto.Observability.

**B) K8s CronJob (внешний триггер):**
```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: data-collection
spec:
  schedule: "0 8 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: collector
            image: data-collector:latest
            args: ["--mode", "batch"]
```
Плюс: K8s управляет жизненным циклом, retries, parallelism.

## Koto использует

- `Koto.Scheduling` — `BatchJobBase<T>`, distributed lock, progress tracking
- `Koto.Messaging.Wolverine` — Kafka pipeline между сервисами
- `Koto.Observability` — job duration metrics, batch progress logs

## Структура

```
samples/DataPipeline/
  src/
    DataCollectorService/
    DataTransformerService/
    ReportGeneratorService/
    NotificationService/
  infra/
    docker-compose.yml
    k8s/
      cronjob.yaml              ← K8s CronJob вариант
      hpa.yaml                  ← HPA для transformer (CPU-based OK здесь)
```
