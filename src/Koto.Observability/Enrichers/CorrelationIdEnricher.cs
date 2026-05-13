using Serilog.Core;
using Serilog.Events;

namespace Koto.Observability.Enrichers;

/// <summary>
/// Serilog enricher that adds a <c>CorrelationId</c> property to every log event.
/// The value is obtained by calling the <see cref="ObservabilityOptions.CorrelationIdProvider"/> delegate.
/// No property is added when the provider returns <c>null</c>.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly Func<string?> _provider;

    /// <summary>Initializes the enricher with the given correlation ID provider.</summary>
    public CorrelationIdEnricher(Func<string?> provider) => _provider = provider;

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _provider();
        if (correlationId is null) return;

        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}
