using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Koto.Api.AspNetCore;

/// <summary>DI registration helpers for <c>Koto.Api.AspNetCore</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KotoApiOptions"/> (error → status classifier, guard mode), a
    /// <see cref="TimeProvider"/> for envelope timestamps (unless one is already registered) and the
    /// <see cref="EnrichmentGuard"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional customization, e.g. <c>o =&gt; o.Map("payments.gateway.failed", 502)</c>.</param>
    public static IServiceCollection AddKotoAspNetCore(
        this IServiceCollection services,
        Action<KotoApiOptions>? configure = null)
    {
        var builder = services.AddOptions<KotoApiOptions>();
        if (configure is not null)
            builder.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<EnrichmentGuard>();
        return services;
    }
}
