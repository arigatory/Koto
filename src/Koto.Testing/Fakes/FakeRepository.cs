using System.Collections.Concurrent;
using Koto.Application;
using Koto.Domain;

namespace Koto.Testing.Fakes;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IRepository{TAgg,TId}"/> for use in tests.
/// </summary>
/// <typeparam name="TAgg">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate ID type.</typeparam>
public sealed class FakeRepository<TAgg, TId> : IRepository<TAgg, TId>
    where TAgg : AggregateRoot<TId>
    where TId : notnull
{
    private readonly ConcurrentDictionary<TId, TAgg> _store = new();

    /// <summary>All aggregates currently in the repository.</summary>
    public IReadOnlyCollection<TAgg> All => _store.Values.ToList();

    /// <inheritdoc/>
    public void Add(TAgg aggregate) => _store[aggregate.Id] = aggregate;

    /// <inheritdoc/>
    public void Delete(TAgg aggregate) => _store.TryRemove(aggregate.Id, out _);

    /// <inheritdoc/>
    public Task<TAgg?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var aggregate);
        return Task.FromResult(aggregate);
    }
}
