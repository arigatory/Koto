using AwesomeAssertions;
using Koto.Observability;
using Microsoft.Extensions.Hosting;

namespace Koto.Observability.Tests;

public class ObservabilitySetupTests
{
    [Fact]
    public void AddKotoObservability_does_not_throw_with_defaults()
    {
        var builder = Host.CreateApplicationBuilder();

        var act = () => builder.AddKotoObservability();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddKotoObservability_does_not_throw_with_all_options_set()
    {
        var builder = Host.CreateApplicationBuilder();

        var act = () => builder.AddKotoObservability(opts =>
        {
            opts.ServiceName = "test-service";
            opts.OtlpEndpoint = "http://localhost:4317";
opts.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
            opts.CorrelationIdProvider = () => "corr-test";
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void AddKotoObservability_returns_same_builder()
    {
        var builder = Host.CreateApplicationBuilder();

        var returned = builder.AddKotoObservability();

        returned.Should().BeSameAs(builder);
    }

    [Fact]
    public void ObservabilityOptions_defaults_to_information_level()
    {
        var opts = new ObservabilityOptions();

        opts.MinimumLevel.Should().Be(Serilog.Events.LogEventLevel.Information);
    }

    [Fact]
    public void ObservabilityOptions_service_name_falls_back_to_unknown_service()
    {
        // Ensure env var is not set for this test
        var original = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", null);

        try
        {
            var opts = new ObservabilityOptions();
            opts.ServiceName.Should().Be("unknown-service");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", original);
        }
    }
}
