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

    // Behaviors are resolved against the CONCRETE command type (not the ICommand<T> marker),
    // so test behaviors implement IPipelineBehavior<TheCmd, Result<int>>.
    private sealed class FirstBehavior(OrderTracker tracker)
        : IPipelineBehavior<TheCmd, Result<int>>
    {
        public async Task<Result<int>> HandleAsync(
            TheCmd request, Func<Task<Result<int>>> next, CancellationToken ct)
        {
            tracker.Order.Add("first:before");
            var result = await next();
            tracker.Order.Add("first:after");
            return result;
        }
    }

    private sealed class SecondBehavior(OrderTracker tracker)
        : IPipelineBehavior<TheCmd, Result<int>>
    {
        public async Task<Result<int>> HandleAsync(
            TheCmd request, Func<Task<Result<int>>> next, CancellationToken ct)
        {
            tracker.Order.Add("second:before");
            var result = await next();
            tracker.Order.Add("second:after");
            return result;
        }
    }

    // Captures the closed TRequest type an open-generic behavior sees at dispatch time.
    private sealed class TypeCapturingBehavior<TRequest, TResponse>(OrderTracker tracker)
        : IPipelineBehavior<TRequest, TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
        {
            tracker.Order.Add($"request-type:{typeof(TRequest).Name}");
            return next();
        }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public List<string> Calls { get; } = [];
        public bool HasActiveTransaction { get; private set; }
        public Task BeginTransactionAsync(CancellationToken ct = default) { Calls.Add("begin"); HasActiveTransaction = true; return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken ct = default) { Calls.Add("commit"); HasActiveTransaction = false; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken ct = default) { Calls.Add("rollback"); HasActiveTransaction = false; return Task.CompletedTask; }
    }

    /// <summary>Outer command whose handler dispatches an inner command through the pipeline.</summary>
    private sealed record OuterCmd : ICommand<int>;
    private sealed class OuterCmdHandler(ICqrsDispatcher dispatcher) : ICommandHandler<OuterCmd, int>
    {
        public async Task<Result<int>> HandleAsync(OuterCmd cmd, CancellationToken ct = default) =>
            await dispatcher.SendAsync<int>(new TheCmd(21), ct);
    }

    private sealed record TheQuery(int Value) : IQuery<int>;
    private sealed class TheQueryHandler : IQueryHandler<TheQuery, int>
    {
        public Task<Result<int>> HandleAsync(TheQuery q, CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Success(q.Value));
    }

    private sealed record FailCmd : ICommand<int>;
    private sealed class FailCmdHandler : ICommandHandler<FailCmd, int>
    {
        public Task<Result<int>> HandleAsync(FailCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Failure(new Error("test.command.failed", "nope")));
    }

    private sealed record ThrowCmd : ICommand<int>;
    private sealed class ThrowCmdHandler : ICommandHandler<ThrowCmd, int>
    {
        public Task<Result<int>> HandleAsync(ThrowCmd cmd, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");
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
            s.AddSingleton<IPipelineBehavior<TheCmd, Result<int>>>(new FirstBehavior(tracker));
            s.AddSingleton<IPipelineBehavior<TheCmd, Result<int>>>(new SecondBehavior(tracker));
        });

        await dispatcher.SendAsync<int>(new TheCmd(3));

        tracker.Order.Should().ContainInOrder("first:before", "second:before", "second:after", "first:after");
    }

    [Fact]
    public async Task Nested_command_dispatch_joins_the_ambient_transaction()
    {
        var uow = new FakeUow();
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<OuterCmd, int>, OuterCmdHandler>();
            s.AddTransient<ICommandHandler<TheCmd, int>, TheCmdHandler>();
            s.AddSingleton<IUnitOfWork>(uow);
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        });

        var result = await dispatcher.SendAsync<int>(new OuterCmd());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        // Exactly one transaction — the inner command joined the outer one
        uow.Calls.Should().Equal("begin", "commit");
    }

    [Fact]
    public async Task Open_generic_behavior_closes_over_concrete_command_type()
    {
        var tracker = new OrderTracker();
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<TheCmd, int>, TheCmdHandler>();
            s.AddSingleton(tracker);
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(TypeCapturingBehavior<,>));
        });

        await dispatcher.SendAsync<int>(new TheCmd(3));

        tracker.Order.Should().ContainSingle().Which.Should().Be($"request-type:{nameof(TheCmd)}");
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

    [Fact]
    public async Task TransactionBehavior_rolls_back_on_failure_result()
    {
        var uow = new FakeUow();
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<FailCmd, int>, FailCmdHandler>();
            s.AddSingleton<IUnitOfWork>(uow);
            s.AddSingleton(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        });

        var result = await dispatcher.SendAsync<int>(new FailCmd());

        result.IsFailure.Should().BeTrue();
        uow.Calls.Should().ContainInOrder("begin", "rollback");
        uow.Calls.Should().NotContain("commit");
    }

    [Fact]
    public async Task TransactionBehavior_rolls_back_and_rethrows_on_exception()
    {
        var uow = new FakeUow();
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<ThrowCmd, int>, ThrowCmdHandler>();
            s.AddSingleton<IUnitOfWork>(uow);
            s.AddSingleton(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        });

        var act = async () => await dispatcher.SendAsync<int>(new ThrowCmd());

        await act.Should().ThrowAsync<InvalidOperationException>();
        uow.Calls.Should().ContainInOrder("begin", "rollback");
        uow.Calls.Should().NotContain("commit");
    }
}
