using Koto.Application;
using Koto.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine.EntityFrameworkCore;

namespace Koto.Infrastructure.EFCore;

/// <summary>DI registration helpers for <c>Koto.Infrastructure.EFCore</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> via Wolverine's EF Core integration
    /// (outbox in the same transaction) and the generic <see cref="Repository{TAgg,TId}"/>.
    /// </summary>
    /// <remarks>
    /// Also call <c>opts.PublishDomainEventsFromEntityFrameworkCore&lt;IHasDomainEvents, IDomainEvent&gt;(e => e.DomainEvents)</c>
    /// in your <c>WolverineOptions</c> to enable automatic domain event dispatch.
    /// </remarks>
    public static IServiceCollection AddKotoEFCore<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : KotoDbContext
    {
        services.AddDbContextWithWolverineIntegration<TContext>(configure);
        // Repository<,> depends on the abstract KotoDbContext; forward it to the concrete context
        // (TryAdd — with several contexts in one service the first registered one backs the generic repository).
        services.TryAddScoped<KotoDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        // Default unit of work: commits publish domain events through the Wolverine outbox.
        // TryAdd — a hand-written IUnitOfWork registered by the consumer wins.
        services.TryAddScoped<IUnitOfWork, EfCoreUnitOfWork<TContext>>();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TContext"/> for a service that does <b>not</b> run Wolverine: a plain
    /// <c>AddDbContext</c>, the generic <see cref="Repository{TAgg,TId}"/> and <see cref="EfCoreUnitOfWork{TContext}"/>
    /// as the default <see cref="IUnitOfWork"/> — the three registrations such a service would otherwise write by hand.
    /// </summary>
    /// <remarks>
    /// There is no outbox, so domain events have nowhere to go: <see cref="EfCoreUnitOfWork{TContext}.CommitAsync"/>
    /// throws when a tracked aggregate raised one, instead of saving and dropping it. Use this for services whose
    /// aggregates raise no domain events; once they do, switch to <see cref="AddKotoEFCore{TContext}"/> with Wolverine
    /// or register your own <see cref="IUnitOfWork"/> before this call (TryAdd — yours wins).
    /// </remarks>
    public static IServiceCollection AddKotoEFCoreWithoutOutbox<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : KotoDbContext
    {
        services.AddDbContext<TContext>(configure);
        services.TryAddScoped<KotoDbContext>(sp => sp.GetRequiredService<TContext>());
        services.TryAddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.TryAddScoped<IUnitOfWork, EfCoreUnitOfWork<TContext>>();
        return services;
    }
}
