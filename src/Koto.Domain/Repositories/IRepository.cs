namespace Koto.Domain;

/// <summary>
/// Persistence contract for aggregate roots. Implementations live in the infrastructure
/// layer; this interface belongs to the domain layer to keep aggregates independent of
/// any specific ORM or database.
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
