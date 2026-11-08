using System.Reflection;
using AwesomeAssertions;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Application.Tests;

public class HandlerValidationTests
{
    private sealed record Orphan : ICommand<int>;

    private sealed record Twice(int X) : IQuery<int>;

    private sealed class TwiceHandlerA : IQueryHandler<Twice, int>
    {
        public Task<AppResult<int>> HandleAsync(Twice query, CancellationToken ct = default) =>
            Task.FromResult(AppResult<int>.Success(1));
    }

    private sealed class TwiceHandlerB : IQueryHandler<Twice, int>
    {
        public Task<AppResult<int>> HandleAsync(Twice query, CancellationToken ct = default) =>
            Task.FromResult(AppResult<int>.Success(2));
    }

    private static readonly Assembly TestAssembly = typeof(HandlerValidationTests).Assembly;

    [Fact]
    public void Throws_when_command_has_no_handler()
    {
        var act = () => new ServiceCollection().AddKotoApplication(TestAssembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(Orphan).FullName}: no handler*");
    }

    [Fact]
    public void Throws_when_query_has_two_handlers()
    {
        var act = () => new ServiceCollection().AddKotoApplication(TestAssembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(Twice).FullName}: 2 handlers*")
            .WithMessage($"*{nameof(TwiceHandlerA)}*")
            .WithMessage("*ValidateHandlers*");
    }

    [Fact]
    public void Validation_can_be_disabled()
    {
        var act = () => new ServiceCollection().AddKotoApplication(o => o.ValidateHandlers = false, TestAssembly);

        act.Should().NotThrow();
    }

    [Fact]
    public void Registers_scanned_assemblies_singleton_aggregating_calls()
    {
        var services = new ServiceCollection();
        services.AddKotoApplication(o => o.ValidateHandlers = false, TestAssembly);
        services.AddKotoApplication(o => o.ValidateHandlers = false, typeof(Error).Assembly);

        var assemblies = services.BuildServiceProvider().GetRequiredService<KotoApplicationAssemblies>();

        assemblies.Assemblies.Should().BeEquivalentTo([TestAssembly, typeof(Error).Assembly]);
    }

    [Fact]
    public void Calling_AddKotoApplication_twice_does_not_duplicate_behaviors_or_dispatcher()
    {
        var services = new ServiceCollection();
        services.AddKotoApplication(o => { o.AddLoggingBehavior(); o.ValidateHandlers = false; }, TestAssembly);
        services.AddKotoApplication(o => { o.AddLoggingBehavior(); o.ValidateHandlers = false; }, typeof(Error).Assembly);

        services.Count(d => d.ServiceType == typeof(IPipelineBehavior<,>) && d.ImplementationType == typeof(LoggingBehavior<,>)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(ICqrsDispatcher)).Should().Be(1);
    }
}
