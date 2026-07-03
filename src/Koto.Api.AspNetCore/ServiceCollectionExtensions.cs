using Microsoft.Extensions.DependencyInjection;

namespace Koto.Api.AspNetCore;

/// <summary>DI registration helpers for <c>Koto.Api.AspNetCore</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KotoHttpErrorOptions"/> (the Error → HTTP status registry used by
    /// <c>ToHttpResult</c>/<c>ToActionResult</c> and Problem Details factories).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional customization, e.g. <c>o =&gt; o.Map("payments.gateway-failed", 502)</c>.</param>
    public static IServiceCollection AddKotoAspNetCore(
        this IServiceCollection services,
        Action<KotoHttpErrorOptions>? configure = null)
    {
        var builder = services.AddOptions<KotoHttpErrorOptions>();
        if (configure is not null)
            builder.Configure(configure);
        return services;
    }
}
