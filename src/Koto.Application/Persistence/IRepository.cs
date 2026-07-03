using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Persistence contract for aggregate roots. Lives in the application layer — its only
/// consumers are command/query handlers; aggregates themselves never touch repositories.
/// Implementations live in the infrastructure layer, keeping the domain free of any
/// persistence concern.
/// </summary>
/// <typeparam name="TAgg">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public interface IRepository<TAgg, TId>
    where TAgg : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Returns the aggregate with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Registers the aggregate for insertion. The actual write happens when the
    /// Unit of Work commits (e.g. <c>DbContext.SaveChangesAsync</c>).
    /// </summary>
    void Add(TAgg aggregate);

    /// <summary>
    /// Registers the aggregate for deletion. The actual write happens when the
    /// Unit of Work commits.
    /// </summary>
    void Delete(TAgg aggregate);
}
