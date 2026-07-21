using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.EventSourcing.Marten;

/// <summary>DI registration helpers for <c>Koto.EventSourcing.Marten</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures Marten with the given <paramref name="connectionString"/> and registers
    /// <see cref="IEventSourcedRepository{TAgg,TId}"/> → <see cref="MartenEventSourcedRepository{TAgg,TId}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configure">Optional additional Marten configuration.</param>
    public static IServiceCollection AddKotoMarten(
        this IServiceCollection services,
        string connectionString,
        Action<StoreOptions>? configure = null)
    {
        services.AddMarten(opts =>
        {
            opts.Connection(connectionString);
            opts.AutoCreateSchemaObjects = AutoCreate.All;
            configure?.Invoke(opts);
        });

        services.AddScoped(
            typeof(IEventSourcedRepository<,>),
            typeof(MartenEventSourcedRepository<,>));
        services.AddScoped<MartenAggregateTracker>();
        // TryAdd: своя реализация потребителя выигрывает.
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
            .TryAddScoped<Koto.Application.IUnitOfWork, MartenUnitOfWork>(services);

        return services;
    }
}
