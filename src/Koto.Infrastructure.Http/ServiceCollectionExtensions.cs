using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        => services.AddServiceHttpClient<TInterface, TImplementation>(name, baseUrl, configure: null);

    /// <summary>
    /// Регистрация типизированного клиента с опциями — в т.ч. s2s-ключом
    /// (<see cref="ServiceHttpClientOptions.ApiKey"/>), который проверяет принимающая
    /// сторона схемой <c>ServiceKey</c> (Koto.Api.AspNetCore).
    /// </summary>
    public static IServiceCollection AddServiceHttpClient<TInterface, TImplementation>(
        this IServiceCollection services,
        string name,
        string baseUrl,
        Action<ServiceHttpClientOptions>? configure)
        where TInterface : class
        where TImplementation : ServiceHttpClient, TInterface
    {
        var options = new ServiceHttpClientOptions();
        configure?.Invoke(options);

        // Дефолтный no-op accessor: клиент работает из коробки; приложение может подменить.
        services.TryAddSingleton<ICorrelationIdAccessor, NullCorrelationIdAccessor>();
        services.AddTransient<CorrelationIdHandler>();

        var httpClientBuilder = services.AddHttpClient<TImplementation>(name, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>();

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            httpClientBuilder.AddHttpMessageHandler(
                () => new ServiceKeyHandler(options.ApiKeyHeaderName, options.ApiKey));
        }

        httpClientBuilder.AddStandardResilienceHandler();

        services.AddScoped<TInterface>(sp => sp.GetRequiredService<TImplementation>());

        return services;
    }
}
