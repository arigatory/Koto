using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koto.Api.AspNetCore.Tests;

public class EnrichmentGuardTests
{
    private sealed record Full(int Capacity) : Error("test.guard.full", "Full.");
    private sealed record FullWithAlternative(Full Cause, Guid AlternativeId) : EnrichedError(Cause);
    private sealed record FullForWaitlist(Full Cause, int Position) : EnrichedError(Cause);
    private sealed record Other() : Error("test.guard.other", "Other.");

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger : ILogger<EnrichmentGuard>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static KotoApplicationAssemblies Catalog()
    {
        var services = new ServiceCollection();
        services.AddKotoApplication(o => o.ValidateHandlers = false, typeof(EnrichmentGuardTests).Assembly);
        return services.BuildServiceProvider().GetRequiredService<KotoApplicationAssemblies>();
    }

    private static EnrichmentGuard Guard(EnrichmentGuardMode? mode, IHostEnvironment? environment = null, ILogger<EnrichmentGuard>? logger = null) =>
        new(Options.Create(new KotoApiOptions { EnrichmentGuard = mode }), Catalog(), environment, logger);

    [Fact]
    public void Throws_in_throw_mode_when_fuller_type_exists()
    {
        var act = () => Guard(EnrichmentGuardMode.Throw).Check([new Full(3).Enrich()]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(FullWithAlternative)}*")
            .WithMessage("*test.guard.full*");
    }

    [Fact]
    public void Passes_when_fuller_type_is_used()
    {
        var act = () => Guard(EnrichmentGuardMode.Throw).Check([new FullWithAlternative(new Full(3), Guid.NewGuid())]);

        act.Should().NotThrow();
    }

    [Fact]
    public void Passes_for_errors_without_fuller_type()
    {
        var act = () => Guard(EnrichmentGuardMode.Throw).Check([new Other().ForField("x")]);

        act.Should().NotThrow();
    }

    [Fact]
    public void Warn_mode_only_logs()
    {
        var logger = new ListLogger();

        Guard(EnrichmentGuardMode.Warn, logger: logger).Check([new Full(3).Enrich()]);

        logger.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Off_mode_ignores_violations()
    {
        var logger = new ListLogger();

        Guard(EnrichmentGuardMode.Off, logger: logger).Check([new Full(3).Enrich()]);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Default_mode_is_throw_in_development_and_warn_elsewhere()
    {
        Guard(null, new FakeEnvironment("Development")).Mode.Should().Be(EnrichmentGuardMode.Throw);
        Guard(null, new FakeEnvironment("Production")).Mode.Should().Be(EnrichmentGuardMode.Warn);
        Guard(null).Mode.Should().Be(EnrichmentGuardMode.Warn);
    }

    [Fact]
    public void Without_scanned_assemblies_the_guard_is_off()
    {
        var guard = new EnrichmentGuard(Options.Create(new KotoApiOptions { EnrichmentGuard = EnrichmentGuardMode.Throw }));

        var act = () => guard.Check([new Full(3).Enrich()]);

        act.Should().NotThrow();
    }

    [Fact]
    public void Passes_when_any_of_several_fuller_types_is_used()
    {
        var guard = Guard(EnrichmentGuardMode.Throw);

        var act = () => guard.Check([
            new FullWithAlternative(new Full(3), Guid.NewGuid()),
            new FullForWaitlist(new Full(3), 2)]);

        act.Should().NotThrow();
    }
}
