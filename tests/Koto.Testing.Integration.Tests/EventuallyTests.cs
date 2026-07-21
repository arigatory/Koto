using AwesomeAssertions;
using Koto.Application;
using Koto.Testing.Integration;

namespace Koto.Testing.Integration.Tests;

public sealed class EventuallyTests
{
    [Fact]
    public async Task Returns_when_probe_becomes_true()
    {
        var attempts = 0;

        await Eventually.AssertAsync(
            () => Task.FromResult(++attempts >= 3),
            TimeSpan.FromSeconds(10), "third attempt", TimeSpan.FromMilliseconds(10));

        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Timeout_message_names_what_was_awaited()
    {
        var act = () => Eventually.AssertAsync(
            () => Task.FromResult(false),
            TimeSpan.FromMilliseconds(50), "the impossible", TimeSpan.FromMilliseconds(10));

        (await act.Should().ThrowAsync<TimeoutException>())
            .WithMessage("*the impossible*");
    }
}

public sealed class IntegrationEventTopicsTests
{
    private sealed record WithTopic(Guid Id) : IntegrationEvent
    {
        public const string Topic = "tests.with-topic";
    }

    private sealed record WithoutTopic(Guid Id) : IntegrationEvent;

    [Fact]
    public void Resolves_topic_constant() =>
        IntegrationEventTopics.For<WithTopic>().Should().Be("tests.with-topic");

    [Fact]
    public void Missing_topic_fails_fast()
    {
        var act = () => IntegrationEventTopics.For<WithoutTopic>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Topic*");
    }
}
