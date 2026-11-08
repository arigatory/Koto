using AwesomeAssertions;
using FluentValidation;
using Koto.Application;
using Koto.Domain;
using Koto.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koto.Validation.Tests;

public class ServiceCollectionExtensionsTests
{
    private sealed record OrderedCmd(string Name) : ICommand<string>;

    private sealed class OrderedHandler : ICommandHandler<OrderedCmd, string>
    {
        public Task<AppResult<string>> HandleAsync(OrderedCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(AppResult<string>.Success(cmd.Name));
    }

    private sealed class OrderedValidator : AbstractValidator<OrderedCmd>
    {
        public OrderedValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public List<string> Calls { get; } = [];
        public bool HasActiveTransaction { get; private set; }
        public Task BeginTransactionAsync(CancellationToken ct = default) { Calls.Add("begin"); HasActiveTransaction = true; return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken ct = default) { Calls.Add("commit"); HasActiveTransaction = false; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken ct = default) { Calls.Add("rollback"); HasActiveTransaction = false; return Task.CompletedTask; }
    }

    private static readonly System.Reflection.Assembly TestAssembly = typeof(ServiceCollectionExtensionsTests).Assembly;

    private static IEnumerable<Type?> BehaviorRegistrations(IServiceCollection services) =>
        services.Where(d => d.ServiceType == typeof(IPipelineBehavior<,>)).Select(d => d.ImplementationType);

    [Fact]
    public void AddValidationBehavior_then_AddKotoValidation_registers_behavior_once_in_configured_order()
    {
        var services = new ServiceCollection();
        services.AddKotoApplication(
            o => { o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(); o.ValidateHandlers = false; },
            TestAssembly);
        services.AddKotoValidation(TestAssembly);

        BehaviorRegistrations(services).Should().Equal(
            typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(TransactionBehavior<,>));
        services.Should().Contain(d => d.ServiceType == typeof(IValidator<OrderedCmd>));
    }

    [Fact]
    public void AddKotoValidation_alone_registers_the_behavior()
    {
        var services = new ServiceCollection();
        services.AddKotoValidation(TestAssembly);

        BehaviorRegistrations(services).Should().Equal(typeof(ValidationBehavior<,>));
    }

    [Fact]
    public async Task Validation_failure_does_not_open_a_transaction_when_ordered_before_it()
    {
        var uow = new RecordingUnitOfWork();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IUnitOfWork>(uow);
        services.AddKotoApplication(
            o => { o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(); o.ValidateHandlers = false; },
            TestAssembly);
        services.AddKotoValidation(TestAssembly);

        var dispatcher = services.BuildServiceProvider().GetRequiredService<ICqrsDispatcher>();
        var result = await dispatcher.SendAsync<string>(new OrderedCmd(""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Field.Should().Be("Name");
        uow.Calls.Should().BeEmpty();
    }

    [Fact]
    public void AddKotoValidation_before_AddKotoApplication_still_registers_the_behavior_once()
    {
        var services = new ServiceCollection();
        services.AddKotoValidation(TestAssembly);
        services.AddKotoApplication(
            o => { o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(); o.ValidateHandlers = false; },
            TestAssembly);

        BehaviorRegistrations(services).Count(t => t == typeof(ValidationBehavior<,>)).Should().Be(1);
    }
}
