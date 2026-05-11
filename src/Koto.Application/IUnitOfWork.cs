namespace Koto.Application;

/// <summary>
/// Abstraction for database transaction management. Implemented by the infrastructure layer
/// (e.g. <c>Koto.Infrastructure.EFCore</c>). Used by <see cref="TransactionBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Begins a new database transaction.</summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Commits the current transaction.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls back the current transaction.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
