using Koto.Application;
using Koto.Messaging.Wolverine.Consuming;
using Koto.Messaging.Wolverine.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Koto.Messaging.Wolverine;

/// <summary>DI registration helpers for <c>Koto.Messaging.Wolverine</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Wolverine-backed implementations of Koto messaging abstractions.
    /// </summary>
    /// <remarks>
    /// Still call <c>host.UseWolverine(opts => { ... })</c> separately to configure
    /// Kafka transport, topic routing, and error policies (DLQ, retries).
    /// Example Wolverine setup:
    /// <code>
    /// host.UseWolverine(opts =>
    /// {
    ///     opts.UseKafka("bootstrap-servers:9092")
    ///         .AutoProvisionTopics();
    ///
    ///     opts.PublishMessage&lt;OrderPlacedEvent&gt;()
    ///         .ToKafkaTopic("orders.order-placed");
    ///
    ///     opts.ListenToKafkaTopic("payments.payment-processed")
    ///         .ProcessInline();
    ///
    ///     opts.Policies.AddMiddleware&lt;CorrelationIdMiddleware&gt;();
    /// });
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for <see cref="KotoWolverineOptions"/>.</param>
    public static IServiceCollection AddKotoWolverine(
        this IServiceCollection services,
        Action<KotoWolverineOptions>? configure = null)
    {
        services.Configure<KotoWolverineOptions>(opts => configure?.Invoke(opts));

        services.AddScoped<IIntegrationEventPublisher, WolverineIntegrationEventPublisher>();
        services.AddScoped<IIntegrationCommandDispatcher, WolverineIntegrationCommandDispatcher>();

        // Default in-memory idempotency store (dev/tests). For production install
        // Koto.Messaging.Wolverine.Postgres and call AddPostgresProcessedMessageStore(...) —
        // TryAdd here makes the durable registration win regardless of call order.
        services.TryAddSingleton<IProcessedMessageStore, InMemoryProcessedMessageStore>();

        return services;
    }
}
