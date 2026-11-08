using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koto.Application.Tests;

public class KotoApplicationOptionsTests
{
    private sealed record PingCmd : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCmd, string>
    {
        public Task<AppResult<string>> HandleAsync(PingCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(AppResult<string>.Success("pong"));
    }

    [Fact]
    public void AddBehavior_rejects_type_that_is_not_a_pipeline_behavior()
    {
        var options = new KotoApplicationOptions();

        var act = () => options.AddBehavior(typeof(string));

        act.Should().Throw<ArgumentException>().WithMessage("*IPipelineBehavior*");
    }

    [Fact]
    public async Task AddKotoApplication_with_options_registers_behaviors_and_handlers()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddKotoApplication(
            o => { o.AddLoggingBehavior(); o.ValidateHandlers = false; },   // the test assembly deliberately contains orphan commands
            typeof(KotoApplicationOptionsTests).Assembly);

        var dispatcher = services.BuildServiceProvider().GetRequiredService<ICqrsDispatcher>();
        var result = await dispatcher.SendAsync<string>(new PingCmd());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("pong");
    }

    [Fact]
    public async Task AddKotoApplication_registers_no_behaviors_unless_configured()
    {
        var services = new ServiceCollection();
        services.AddKotoApplication(o => o.ValidateHandlers = false, typeof(KotoApplicationOptionsTests).Assembly);

        services.Should().NotContain(d => d.ServiceType == typeof(IPipelineBehavior<,>));

        var dispatcher = services.BuildServiceProvider().GetRequiredService<ICqrsDispatcher>();
        var result = await dispatcher.SendAsync<string>(new PingCmd());
        result.Value.Should().Be("pong");
    }
}
