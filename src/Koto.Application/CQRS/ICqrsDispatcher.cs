using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Dispatches commands and queries to their handlers, running them through the
/// registered <see cref="IPipelineBehavior{TRequest,TResponse}"/> chain.
/// </summary>
public interface ICqrsDispatcher
{
    /// <summary>Sends a void command through the pipeline.</summary>
    Task<Result<Unit>> SendAsync(ICommand command, CancellationToken ct = default);

    /// <summary>Sends a result-bearing command through the pipeline.</summary>
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);

    /// <summary>Dispatches a query through the pipeline.</summary>
    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
