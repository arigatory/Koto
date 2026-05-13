using AwesomeAssertions;
using Koto.Observability.Enrichers;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Koto.Observability.Tests;

public class CorrelationIdEnricherTests
{
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    [Fact]
    public void Adds_correlation_id_property_when_provider_returns_value()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new CorrelationIdEnricher(() => "corr-abc"))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("test");

        sink.Events.Should().ContainSingle();
        sink.Events[0].Properties.Should().ContainKey("CorrelationId");
        sink.Events[0].Properties["CorrelationId"].ToString().Should().Contain("corr-abc");
    }

    [Fact]
    public void Does_not_add_property_when_provider_returns_null()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new CorrelationIdEnricher(() => null))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("test");

        sink.Events.Should().ContainSingle();
        sink.Events[0].Properties.Should().NotContainKey("CorrelationId");
    }

    [Fact]
    public void Adds_property_for_every_log_event()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new CorrelationIdEnricher(() => "corr-xyz"))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("first");
        logger.Warning("second");

        sink.Events.Should().HaveCount(2);
        sink.Events.Should().AllSatisfy(e =>
            e.Properties.Should().ContainKey("CorrelationId"));
    }
}
