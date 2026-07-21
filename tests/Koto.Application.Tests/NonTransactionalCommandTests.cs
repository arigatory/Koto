using AwesomeAssertions;
using Koto.Domain;
using NSubstitute;

namespace Koto.Application.Tests;

public sealed class NonTransactionalCommandTests
{
    private sealed record RegularCommand : ICommand<int>;

    private sealed record CounterCommand : ICommand<int>, INonTransactionalCommand;

    [Fact]
    public async Task Regular_command_is_wrapped_in_transaction()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<RegularCommand, Result<int>>(uow);

        await behavior.HandleAsync(new RegularCommand(), () => Task.FromResult(Result<int>.Success(1)), default);

        await uow.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_transactional_command_bypasses_the_unit_of_work()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<CounterCommand, Result<int>>(uow);

        var result = await behavior.HandleAsync(
            new CounterCommand(), () => Task.FromResult(Result<int>.Success(7)), default);

        result.Value.Should().Be(7);
        await uow.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_transactional_command_failure_is_not_rolled_back()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<CounterCommand, Result<int>>(uow);

        var result = await behavior.HandleAsync(
            new CounterCommand(),
            () => Task.FromResult(Result<int>.Failure(new Error("otp.invalid", "bad code"))),
            default);

        result.IsFailure.Should().BeTrue();
        await uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }
}
