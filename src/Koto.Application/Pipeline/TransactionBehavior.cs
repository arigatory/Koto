namespace Koto.Application;

/// <summary>
/// Pipeline behavior that wraps commands in a database transaction via <see cref="IUnitOfWork"/>.
/// Queries (<see cref="IQuery{TResult}"/>) are passed through unchanged.
/// Commits on success, rolls back if an exception is thrown.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private static readonly bool IsCommand =
        typeof(ICommandBase).IsAssignableFrom(typeof(TRequest));

    private readonly IUnitOfWork _uow;

    /// <summary>Initializes the behavior with the provided unit of work.</summary>
    public TransactionBehavior(IUnitOfWork uow) => _uow = uow;

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (!IsCommand)
            return await next();

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var response = await next();
            await _uow.CommitAsync(ct);
            return response;
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}
