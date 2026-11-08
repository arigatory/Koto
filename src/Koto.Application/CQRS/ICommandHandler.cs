using Koto.Domain;

namespace Koto.Application;

/// <summary>Handles a command that produces no return value.</summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <summary>Handles <paramref name="command"/> and returns <see cref="AppResult{T}"/> of <see cref="Unit"/>.</summary>
    Task<AppResult<Unit>> HandleAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>Handles a command that produces a result value.</summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The type of the success value.</typeparam>
public interface ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>Handles <paramref name="command"/> and returns <see cref="AppResult{T}"/> of <typeparamref name="TResult"/>.</summary>
    Task<AppResult<TResult>> HandleAsync(TCommand command, CancellationToken ct = default);
}
