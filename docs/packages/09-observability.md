# Koto.Observability — Plan

**Phase:** 4 | **Status:** NOT STARTED
**Depends on:** Serilog + Serilog.Sinks.OpenTelemetry + OpenTelemetry .NET SDK

---

## Цель

One-liner настройка полного observability стека: структурированные логи, distributed tracing, метрики. Всё через OTLP — vendor-agnostic (Grafana, Jaeger, Datadog, etc.).

## Checklist

### Main Setup
- [ ] `AddKotoObservability(IHostApplicationBuilder builder, Action<ObservabilityOptions>? configure = null)`:
  **Serilog:**
  - Console sink (dev), OTLP sink (prod)
  - Enrichers: `CorrelationId`, `MachineName`, `Environment`
  - Min level configurable via env: `KOTO_LOG_LEVEL` (default: Information)

  **OpenTelemetry Traces:**
  - ASP.NET Core instrumentation
  - HttpClient instrumentation
  - EF Core instrumentation
  - Wolverine instrumentation (если есть)
  - OTLP exporter → `OTEL_EXPORTER_OTLP_ENDPOINT` env variable

  **OpenTelemetry Metrics:**
  - ASP.NET Core metrics (request duration, count)
  - .NET Runtime metrics (GC, threadpool)
  - OTLP exporter

### Serilog Enrichers
- [ ] `CorrelationIdEnricher` — добавляет `CorrelationId` из `ICorrelationIdAccessor` в каждый log event
- [ ] `DomainEventEnricher` — обогащает лог при обработке domain events: `AggregateId`, `AggregateType`, `EventType`

### ObservabilityOptions
- [ ] `ObservabilityOptions`:
  - `string ServiceName` — имя сервиса для OTel resource
  - `string? OtlpEndpoint` — переопределение endpoint
  - `bool EnableConsoleExporter` — включить console exporter (для dev)
  - `LogEventLevel MinimumLevel`

## Переменные окружения

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317
OTEL_SERVICE_NAME=orders-service          # или через ObservabilityOptions
KOTO_LOG_LEVEL=Information
```

## Тесты
- Observability — преимущественно smoke tests и конфигурационные проверки
- [ ] AddKotoObservability не throws при корректной конфигурации
- [ ] CorrelationIdEnricher добавляет property в log event
