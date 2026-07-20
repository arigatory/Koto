using Koto.Application;
using Koto.Domain;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.EntityFrameworkCore;

namespace Koto.Infrastructure.EFCore;

/// <summary>
/// Default <see cref="IUnitOfWork"/> over a <see cref="KotoDbContext"/>.
/// <para>
/// On commit, uncommitted domain events from all tracked aggregates are published through
/// Wolverine's EF Core outbox (<see cref="IDbContextOutbox"/>) in the same transaction as the
/// entity changes — this is what makes the README flow
/// (<c>aggregate.DoSomething() → CommitAsync() → outbox → in-process handler</c>) work from
/// plain HTTP endpoints, not only inside Wolverine message handlers.
/// </para>
/// <para>
/// When Wolverine messaging is not registered (no <see cref="IDbContextOutbox"/> in DI) or the
/// aggregates raised no events, commit degrades to a plain <c>SaveChangesAsync</c>.
/// </para>
/// </summary>
/// <typeparam name="TContext">The service's <see cref="KotoDbContext"/>.</typeparam>
public class EfCoreUnitOfWork<TContext> : IUnitOfWork
    where TContext : KotoDbContext
{
    private readonly TContext _context;
    private readonly IServiceProvider _services;
    private IDbContextTransaction? _transaction;

    /// <summary>Initializes the unit of work.</summary>
    public EfCoreUnitOfWork(TContext context, IServiceProvider services)
    {
        _context = context;
        _services = services;
    }

    /// <inheritdoc/>
    public bool HasActiveTransaction => _transaction is not null;

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken ct = default) =>
        _transaction = await _context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        var domainEvents = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var outbox = domainEvents.Count > 0 ? _services.GetService<IDbContextOutbox>() : null;

        if (outbox is null)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            await CommitTransactionIfAnyAsync(ct).ConfigureAwait(false);
            return;
        }

        outbox.Enroll(_context);
        foreach (var domainEvent in domainEvents)
            await outbox.PublishAsync(domainEvent).ConfigureAwait(false);

        if (_transaction is null)
        {
            // Конверты сообщений и изменения сущностей сохраняются одной транзакцией,
            // затем сообщения отправляются.
            await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
            return;
        }

        // Явная транзакция: конверты пишутся тем же SaveChanges, отправка — после коммита.
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        await CommitTransactionIfAnyAsync(ct).ConfigureAwait(false);
        await outbox.FlushOutgoingMessagesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    private async Task CommitTransactionIfAnyAsync(CancellationToken ct)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct).ConfigureAwait(false);
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }
}
