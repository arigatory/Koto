using Koto.Observability.Enrichers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Koto.Observability;

/// <summary>Extension methods for <see cref="IHostApplicationBuilder"/>.</summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Configures Serilog (structured logging with OTLP sink) and OpenTelemetry
    /// (traces + metrics with OTLP exporter) in a single call.
    /// </summary>
    /// <remarks>
    /// Environment variables:
    /// <list type="bullet">
    ///   <item><c>OTEL_SERVICE_NAME</c> — service name for OTel resource</item>
    ///   <item><c>OTEL_EXPORTER_OTLP_ENDPOINT</c> — OTLP collector endpoint (default: http://localhost:4317)</item>
    ///   <item><c>KOTO_LOG_LEVEL</c> — minimum Serilog log level (default: Information)</item>
    /// </list>
    /// </remarks>
    public static IHostApplicationBuilder AddKotoObservability(
        this IHostApplicationBuilder builder,
        Action<ObservabilityOptions>? configure = null)
    {
        var opts = new ObservabilityOptions();
        configure?.Invoke(opts);

        ConfigureSerilog(builder, opts);
        ConfigureOpenTelemetry(builder, opts);

        return builder;
    }

    private static void ConfigureSerilog(IHostApplicationBuilder builder, ObservabilityOptions opts)
    {
        builder.Services.AddSerilog(loggerConfig =>
        {
            loggerConfig
                .MinimumLevel.Is(opts.MinimumLevel)
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.With(new CorrelationIdEnricher(opts.CorrelationIdProvider))
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}" +
                                    "{NewLine}{Exception}")
                .WriteTo.OpenTelemetry(otlp =>
                {
                    otlp.Endpoint = opts.OtlpEndpoint;
                    otlp.Protocol = OtlpProtocol.Grpc;
                    otlp.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = opts.ServiceName
                    };
                });
        });
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder, ObservabilityOptions opts)
    {
        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(opts.ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(opts.OtlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(opts.OtlpEndpoint)));
    }
}
