using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Pipeline behavior that wraps commands in a database transaction via <see cref="IUnitOfWork"/>.
/// Queries (<see cref="IQuery{TResult}"/>) and commands marked with
/// <see cref="INonTransactionalCommand"/> are passed through unchanged.
/// Commits on a successful <see cref="Result{T}"/>; rolls back on a failure <see cref="Result{T}"/>
/// or a thrown exception. A command dispatched from inside another command's handler joins the
/// ambient transaction (when <see cref="IUnitOfWork.HasActiveTransaction"/> reports one) —
/// the outermost command owns the commit/rollback.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private static readonly bool IsCommand =
        typeof(ICommandBase).IsAssignableFrom(typeof(TRequest))
        && !typeof(INonTransactionalCommand).IsAssignableFrom(typeof(TRequest));

    private readonly IUnitOfWork _uow;

    /// <summary>Initializes the behavior with the provided unit of work.</summary>
    public TransactionBehavior(IUnitOfWork uow) => _uow = uow;

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (!IsCommand)
            return await next().ConfigureAwait(false);

        // Nested dispatch: the outermost command already owns a transaction — join it.
        if (_uow.HasActiveTransaction)
            return await next().ConfigureAwait(false);

        await _uow.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var response = await next().ConfigureAwait(false);
            if (response is IResultBase { IsFailure: true })
                // A domain failure (Result.Failure) discards any tracked mutations instead of committing.
                await _uow.RollbackAsync(ct).ConfigureAwait(false);
            else
                await _uow.CommitAsync(ct).ConfigureAwait(false);
            return response;
        }
        catch
        {
            await _uow.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }
}
