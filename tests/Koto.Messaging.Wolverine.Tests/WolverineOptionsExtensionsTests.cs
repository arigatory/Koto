using AwesomeAssertions;
using Wolverine;

namespace Koto.Messaging.Wolverine.Tests;

public sealed class WolverineOptionsExtensionsTests
{
    public sealed record GoodEvent(Guid Id) : Koto.Application.IntegrationEvent
    {
        public const string Topic = "tests.good-event";
    }

    [Fact]
    public void PublishIntegrationEvents_routes_events_with_topic_constant()
    {
        var options = new WolverineOptions();
        options.UseKotoKafka("localhost:9092", "tests");

        var act = () => options.PublishIntegrationEvents(typeof(GoodEvent).Assembly);

        // В этой же сборке ниже есть BadEvent без Topic — конвенция обязана упасть fail-fast.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Topic*");
    }

    [Fact]
    public void UseKotoKafka_rejects_empty_connection_string()
    {
        var options = new WolverineOptions();

        var act = () => options.UseKotoKafka("  ", "tests");

        act.Should().Throw<ArgumentException>();
    }

    public sealed record BadEvent(Guid Id) : Koto.Application.IntegrationEvent;
}
