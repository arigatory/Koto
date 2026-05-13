using Microsoft.Extensions.DependencyInjection;

namespace Koto.Infrastructure.Http;

/// <summary>DI registration helpers for <c>Koto.Infrastructure.Http</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed HTTP client with standard resilience (retry, circuit breaker, timeout)
    /// and optional correlation ID propagation.
    /// </summary>
    /// <typeparam name="TInterface">The application-layer service interface.</typeparam>
    /// <typeparam name="TImplementation">The HTTP client implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Named client identifier.</param>
    /// <param name="baseUrl">Base address for all requests.</param>
    public static IServiceCollection AddServiceHttpClient<TInterface, TImplementation>(
        this IServiceCollection services,
        string name,
        string baseUrl)
        where TInterface : class
        where TImplementation : ServiceHttpClient, TInterface
    {
        services.AddTransient<CorrelationIdHandler>();

        services.AddHttpClient<TImplementation>(name, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddStandardResilienceHandler();

        services.AddScoped<TInterface>(sp => sp.GetRequiredService<TImplementation>());

        return services;
    }
}
