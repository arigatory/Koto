using AwesomeAssertions;
using Koto.Application;
using Koto.Messaging.Wolverine.Publishing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Wolverine;

namespace Koto.Messaging.Wolverine.Tests;

public class WolverineIntegrationCommandDispatcherTests
{
    private sealed record NotifyUser(string UserId) : IIntegrationCommand;
    private sealed record GetUserName(string UserId) : IIntegrationCommand<string>;

    private static IOptions<KotoWolverineOptions> DefaultOptions()
        => Options.Create(new KotoWolverineOptions());

    [Fact]
    public async Task SendAsync_fire_and_forget_delegates_to_bus()
    {
        var bus = Substitute.For<IMessageBus>();
        var dispatcher = new WolverineIntegrationCommandDispatcher(bus, DefaultOptions());
        var command = new NotifyUser("user-1");

        await dispatcher.SendAsync(command);

        await bus.Received(1).SendAsync(command);
    }

    [Fact]
    public async Task SendAsync_request_reply_returns_result_from_bus()
    {
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeAsync<string>(Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns("Ivan");
        var dispatcher = new WolverineIntegrationCommandDispatcher(bus, DefaultOptions());

        var result = await dispatcher.SendAsync<string>(new GetUserName("user-1"));

        result.Should().Be("Ivan");
    }

    [Fact]
    public async Task SendAsync_request_reply_uses_configured_timeout()
    {
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeAsync<string>(Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns("ok");
        var options = Options.Create(new KotoWolverineOptions { RequestReplyTimeout = TimeSpan.FromSeconds(5) });
        var dispatcher = new WolverineIntegrationCommandDispatcher(bus, options);

        await dispatcher.SendAsync<string>(new GetUserName("user-2"));

        await bus.Received(1).InvokeAsync<string>(
            Arg.Any<object>(),
            Arg.Any<CancellationToken>(),
            TimeSpan.FromSeconds(5));
    }
}
