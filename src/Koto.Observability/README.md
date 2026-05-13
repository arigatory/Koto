# Koto.Observability

One-liner observability setup for Koto microservices.

## What's included

- **Serilog** — structured logs with console sink (dev) + OTLP sink (prod)
- **OpenTelemetry Traces** — ASP.NET Core + HttpClient instrumentation → OTLP
- **OpenTelemetry Metrics** — ASP.NET Core + .NET Runtime → OTLP
- **CorrelationIdEnricher** — adds `CorrelationId` to every Serilog event

## Setup

```csharp
// Program.cs
builder.AddKotoObservability(opts =>
{
    opts.ServiceName = "orders-service";
    opts.EnableConsoleExporter = true; // dev only
    // wire up correlation ID from the HTTP middleware
    opts.CorrelationIdProvider = () => CorrelationContext.Current.Value;
});
```

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `OTEL_SERVICE_NAME` | `"unknown-service"` | OTel resource service name |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `"http://localhost:4317"` | OTLP collector endpoint |
| `KOTO_LOG_LEVEL` | `"Information"` | Minimum Serilog log level |

## EF Core instrumentation

`OpenTelemetry.Instrumentation.EntityFrameworkCore` has no stable NuGet release yet.
Add it manually once it ships:

```csharp
// in WithTracing:
tracing.AddEntityFrameworkCoreInstrumentation();
```
