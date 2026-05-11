using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koto.Application.Tests;

public class PipelineBehaviorTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed record TheCmd(int Value) : ICommand<int>;

    private sealed class TheCmdHandler : ICommandHandler<TheCmd, int>
    {
        public Task<Result<int>> HandleAsync(TheCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Success(cmd.Value * 2));
    }

    private sealed class OrderTracker
    {
        public List<string> Order { get; } = [];
    }

    private sealed class FirstBehavior(OrderTracker tracker)
        : IPipelineBehavior<ICommand<int>, Result<int>>
    {
        public async Task<Result<int>> HandleAsync(
            ICommand<int> request, Func<Task<Result<int>>> next, CancellationToken ct)
        {
            tracker.Order.Add("first:before");
            var result = await next();
            tracker.Order.Add("first:after");
            return result;
        }
    }

    private sealed class SecondBehavior(OrderTracker tracker)
        : IPipelineBehavior<ICommand<int>, Result<int>>
    {
        public async Task<Result<int>> HandleAsync(
            ICommand<int> request, Func<Task<Result<int>>> next, CancellationToken ct)
        {
            tracker.Order.Add("second:before");
            var result = await next();
            tracker.Order.Add("second:after");
            return result;
        }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public List<string> Calls { get; } = [];
        public Task BeginTransactionAsync(CancellationToken ct = default) { Calls.Add("begin"); return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken ct = default) { Calls.Add("commit"); return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken ct = default) { Calls.Add("rollback"); return Task.CompletedTask; }
    }

    private sealed record TheQuery(int Value) : IQuery<int>;
    private sealed class TheQueryHandler : IQueryHandler<TheQuery, int>
    {
        public Task<Result<int>> HandleAsync(TheQuery q, CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Success(q.Value));
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
    public async Task Behaviors_execute_in_registration_order_outermost_first()
    {
        var tracker = new OrderTracker();
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<TheCmd, int>, TheCmdHandler>();
            s.AddSingleton<IPipelineBehavior<ICommand<int>, Result<int>>>(new FirstBehavior(tracker));
            s.AddSingleton<IPipelineBehavior<ICommand<int>, Result<int>>>(new SecondBehavior(tracker));
        });

        await dispatcher.SendAsync<int>(new TheCmd(3));

        tracker.Order.Should().ContainInOrder("first:before", "second:before", "second:after", "first:after");
    }

    [Fact]
    public async Task LoggingBehavior_does_not_alter_result()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<TheCmd, int>, TheCmdHandler>();
            s.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            s.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        });

        var result = await dispatcher.SendAsync<int>(new TheCmd(5));

        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task TransactionBehavior_begins_and_commits_for_command()
    {
        var uow = new FakeUow();
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<TheCmd, int>, TheCmdHandler>();
            s.AddSingleton<IUnitOfWork>(uow);
            s.AddSingleton(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        });

        await dispatcher.SendAsync<int>(new TheCmd(1));

        uow.Calls.Should().ContainInOrder("begin", "commit");
    }

    [Fact]
    public async Task TransactionBehavior_skips_transaction_for_query()
    {
        var uow = new FakeUow();
        var dispatcher = Build(s =>
        {
            s.AddTransient<IQueryHandler<TheQuery, int>, TheQueryHandler>();
            s.AddSingleton<IUnitOfWork>(uow);
            s.AddSingleton(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        });

        await dispatcher.QueryAsync<int>(new TheQuery(1));

        uow.Calls.Should().BeEmpty();
    }
}
