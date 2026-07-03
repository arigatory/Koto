using Koto.Application;
using Koto.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        return services;
    }
}
