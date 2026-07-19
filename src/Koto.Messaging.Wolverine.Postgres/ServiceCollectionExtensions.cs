using Koto.Messaging.Wolverine.Consuming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Koto.Messaging.Wolverine.Postgres;

/// <summary>DI registration for the durable PostgreSQL idempotency store.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the in-memory <see cref="IProcessedMessageStore"/> with a durable
    /// PostgreSQL-backed implementation and starts background cleanup of expired entries.
    /// Call order relative to <c>AddKotoWolverine</c> does not matter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string for the deduplication table.</param>
    /// <param name="configure">Optional store configuration (schema, table, cleanup interval).</param>
    public static IServiceCollection AddPostgresProcessedMessageStore(
        this IServiceCollection services,
        string connectionString,
        Action<PostgresProcessedMessageStoreOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Fail fast on invalid identifiers instead of at first message.
        var eager = new PostgresProcessedMessageStoreOptions();
        configure?.Invoke(eager);
        PostgresIdentifier.Validate(eager.Schema, nameof(eager.Schema));
        PostgresIdentifier.Validate(eager.Table, nameof(eager.Table));

        services.Configure<PostgresProcessedMessageStoreOptions>(o => configure?.Invoke(o));

        // Own the data source privately — do not pollute DI with NpgsqlDataSource,
        // the application may register its own (EF Core, Marten).
        services.TryAddSingleton(sp => new StoreDataSource(NpgsqlDataSource.Create(connectionString)));

        services.TryAddSingleton(sp => new PostgresProcessedMessageStore(
            sp.GetRequiredService<StoreDataSource>().DataSource,
            sp.GetRequiredService<IOptions<PostgresProcessedMessageStoreOptions>>(),
            sp.GetRequiredService<IOptions<KotoWolverineOptions>>()));

        services.Replace(ServiceDescriptor.Singleton<IProcessedMessageStore>(
            sp => sp.GetRequiredService<PostgresProcessedMessageStore>()));

        services.AddHostedService<ProcessedMessageCleanupService>();

        return services;
    }

    /// <summary>Disposable holder so the container disposes the privately-owned data source.</summary>
    internal sealed class StoreDataSource(NpgsqlDataSource dataSource) : IAsyncDisposable, IDisposable
    {
        public NpgsqlDataSource DataSource { get; } = dataSource;

        public ValueTask DisposeAsync() => DataSource.DisposeAsync();

        public void Dispose() => DataSource.Dispose();
    }
}
