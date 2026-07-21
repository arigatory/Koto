using Koto.Application;
using Koto.Domain;
using Marten;

namespace Koto.EventSourcing.Marten;

/// <summary>Неголая форма event-sourced агрегата для трекинга в unit of work.</summary>
public interface IEventSourcedAggregate
{
    /// <summary>События, ожидающие сохранения.</summary>
    IReadOnlyList<IDomainEvent> UncommittedEvents { get; }

    /// <summary>Очистка после успешного коммита.</summary>
    void ClearUncommittedEvents();
}

/// <summary>
/// Scoped-трекер агрегатов, застейдженных в текущую Marten-сессию через
/// <c>IEventSourcedRepository.Append</c>: их uncommitted events очищаются
/// после успешного коммита (и только после него).
/// </summary>
public sealed class MartenAggregateTracker
{
    private readonly List<IEventSourcedAggregate> _staged = [];

    /// <summary>Регистрирует агрегат до коммита.</summary>
    public void Track(IEventSourcedAggregate aggregate) => _staged.Add(aggregate);

    /// <summary>Очищает uncommitted events всех отслеживаемых агрегатов (после коммита).</summary>
    public void ClearAll()
    {
        foreach (var aggregate in _staged)
            aggregate.ClearUncommittedEvents();
        _staged.Clear();
    }

    /// <summary>Сбрасывает трекинг без очистки событий (rollback: события не сохранены).</summary>
    public void Reset() => _staged.Clear();
}

/// <summary>
/// <see cref="IUnitOfWork"/> поверх Marten <see cref="IDocumentSession"/>:
/// события нескольких агрегатов и документы (например ledger-операции с
/// идемпотентным OperationId) коммитятся одной транзакцией.
/// <para>
/// Паттерн: <c>repo.Append(a); repo.Append(b); session.Store(doc); await uow.CommitAsync(ct);</c>
/// — либо всё, либо ничего. Совместим с <c>TransactionBehavior</c>.
/// </para>
/// </summary>
public sealed class MartenUnitOfWork(
    IDocumentSession session,
    MartenAggregateTracker tracker) : IUnitOfWork
{
    private bool _active;

    /// <inheritdoc/>
    public bool HasActiveTransaction => _active;

    /// <inheritdoc/>
    public Task BeginTransactionAsync(CancellationToken ct = default)
    {
        // Marten-сессия транзакционна на SaveChanges; явного Begin не требуется.
        _active = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        tracker.ClearAll();
        _active = false;
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken ct = default)
    {
        session.EjectAllPendingChanges();
        tracker.Reset();
        _active = false;
        return Task.CompletedTask;
    }
}
