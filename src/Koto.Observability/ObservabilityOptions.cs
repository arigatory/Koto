using Serilog.Events;

namespace Koto.Observability;

/// <summary>Configuration for <see cref="HostApplicationBuilderExtensions.AddKotoObservability"/>.</summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Service name used as the OpenTelemetry resource attribute.
    /// Defaults to the value of the <c>OTEL_SERVICE_NAME</c> environment variable,
    /// or <c>"unknown-service"</c> if not set.
    /// </summary>
    public string ServiceName { get; set; } =
        Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "unknown-service";

    /// <summary>
    /// OTLP exporter endpoint for both traces and metrics.
    /// Defaults to the <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable,
    /// or <c>"http://localhost:4317"</c> if not set.
    /// </summary>
    public string OtlpEndpoint { get; set; } =
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";

    /// <summary>
    /// Minimum Serilog log level. Defaults to the <c>KOTO_LOG_LEVEL</c> environment variable
    /// (parsed as <see cref="LogEventLevel"/>), or <see cref="LogEventLevel.Information"/>.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = ParseLogLevel(
        Environment.GetEnvironmentVariable("KOTO_LOG_LEVEL"));

    /// <summary>
    /// Optional delegate that returns the current correlation ID to include in every log event.
    /// Example: <c>opts.CorrelationIdProvider = () => CorrelationContext.Current.Value</c>
    /// </summary>
    public Func<string?> CorrelationIdProvider { get; set; } = () => null;

    private static LogEventLevel ParseLogLevel(string? value) =>
        Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;
}
