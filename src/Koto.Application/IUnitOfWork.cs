namespace Koto.Application;

/// <summary>
/// Abstraction for database transaction management. Implemented by the infrastructure layer
/// (e.g. <c>Koto.Infrastructure.EFCore</c>). Used by <see cref="TransactionBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Whether a transaction opened by this unit of work is currently active.
    /// <see cref="TransactionBehavior{TRequest,TResponse}"/> uses this to make nested command
    /// dispatch join the ambient transaction instead of opening a second one.
    /// Defaults to <c>false</c> so existing implementations keep compiling (they simply
    /// never report an ambient transaction).
    /// </summary>
    bool HasActiveTransaction => false;

    /// <summary>Begins a new database transaction.</summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Commits the current transaction.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls back the current transaction.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
