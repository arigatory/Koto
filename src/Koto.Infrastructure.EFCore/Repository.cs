using Koto.Domain;
using Microsoft.EntityFrameworkCore;

namespace Koto.Infrastructure.EFCore;

/// <summary>
/// Generic EF Core repository implementation for aggregate roots.
/// </summary>
public class Repository<TAgg, TId> : IRepository<TAgg, TId>
    where TAgg : AggregateRoot<TId>
    where TId : notnull
{
    private readonly KotoDbContext _context;

    /// <summary>Initializes a new <see cref="Repository{TAgg,TId}"/>.</summary>
    public Repository(KotoDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        _context.Set<TAgg>().FindAsync([id], ct).AsTask()!;

    /// <inheritdoc/>
    public void Add(TAgg aggregate) => _context.Set<TAgg>().Add(aggregate);

    /// <inheritdoc/>
    public void Delete(TAgg aggregate) => _context.Set<TAgg>().Remove(aggregate);
}
