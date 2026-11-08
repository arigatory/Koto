using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koto.Api.AspNetCore;

/// <summary>Resolves Koto API services from the request, with safe defaults when <c>AddKotoAspNetCore</c> was not called.</summary>
public static class KotoHttpContextExtensions
{
    /// <summary>The registered <see cref="KotoApiOptions"/>, or the built-in defaults.</summary>
    public static KotoApiOptions GetKotoApiOptions(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.RequestServices.GetService<IOptions<KotoApiOptions>>()?.Value ?? new KotoApiOptions();
    }

    /// <summary>The registered <see cref="TimeProvider"/>, or <see cref="TimeProvider.System"/>.</summary>
    public static TimeProvider GetKotoTimeProvider(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
    }

    /// <summary>The registered <see cref="EnrichmentGuard"/>, or <c>null</c>.</summary>
    public static EnrichmentGuard? GetEnrichmentGuard(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.RequestServices.GetService<EnrichmentGuard>();
    }
}
