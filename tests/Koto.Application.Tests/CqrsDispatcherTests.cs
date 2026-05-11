using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Application.Tests;

public class CqrsDispatcherTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed record VoidCmd : ICommand;
    private sealed record ResultCmd(int Value) : ICommand<string>;
    private sealed record TheQuery(int Value) : IQuery<string>;

    private sealed class VoidCmdHandler : ICommandHandler<VoidCmd>
    {
        public bool Called { get; private set; }
        public Task<Result<Unit>> HandleAsync(VoidCmd cmd, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }

    private sealed class ResultCmdHandler : ICommandHandler<ResultCmd, string>
    {
        public Task<Result<string>> HandleAsync(ResultCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Success($"value:{cmd.Value}"));
    }

    private sealed class FailingResultCmdHandler : ICommandHandler<ResultCmd, string>
    {
        public Task<Result<string>> HandleAsync(ResultCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Failure(new Error("test.error", "Failed")));
    }

    private sealed class TheQueryHandler : IQueryHandler<TheQuery, string>
    {
        public Task<Result<string>> HandleAsync(TheQuery q, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Success($"query:{q.Value}"));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ICqrsDispatcher Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICqrsDispatcher, CqrsDispatcher>();
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<ICqrsDispatcher>();
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_void_resolves_and_calls_handler()
    {
        var handler = new VoidCmdHandler();
        var dispatcher = Build(s => s.AddSingleton<ICommandHandler<VoidCmd>>(handler));

        var result = await dispatcher.SendAsync(new VoidCmd());

        result.IsSuccess.Should().BeTrue();
        handler.Called.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_result_resolves_and_returns_value()
    {
        var dispatcher = Build(s => s.AddTransient<ICommandHandler<ResultCmd, string>, ResultCmdHandler>());

        var result = await dispatcher.SendAsync<string>(new ResultCmd(42));

        result.Value.Should().Be("value:42");
    }

    [Fact]
    public async Task SendAsync_result_propagates_failure()
    {
        var dispatcher = Build(s => s.AddTransient<ICommandHandler<ResultCmd, string>, FailingResultCmdHandler>());

        var result = await dispatcher.SendAsync<string>(new ResultCmd(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("test.error");
    }

    [Fact]
    public async Task QueryAsync_resolves_and_returns_value()
    {
        var dispatcher = Build(s => s.AddTransient<IQueryHandler<TheQuery, string>, TheQueryHandler>());

        var result = await dispatcher.QueryAsync<string>(new TheQuery(7));

        result.Value.Should().Be("query:7");
    }

    [Fact]
    public async Task Dispatcher_caches_invoker_across_calls()
    {
        var dispatcher = Build(s => s.AddTransient<ICommandHandler<ResultCmd, string>, ResultCmdHandler>());

        var r1 = await dispatcher.SendAsync<string>(new ResultCmd(1));
        var r2 = await dispatcher.SendAsync<string>(new ResultCmd(2));

        r1.Value.Should().Be("value:1");
        r2.Value.Should().Be("value:2");
    }
}
