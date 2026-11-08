using Koto.Api.AspNetCore;
using Koto.Api.FastEndpoints.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Koto.Api.FastEndpoints;

/// <summary>DI and pipeline registration helpers for <c>Koto.Api.FastEndpoints</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Koto API services: <see cref="KotoApiOptions"/> (error → status classifier),
    /// the envelope time provider, the enrichment guard, correlation ID middleware and accessor,
    /// and the global exception handler.
    /// </summary>
    /// <remarks>
    /// Also call <c>app.UseKotoApi()</c> to add the middleware to the pipeline and
    /// <c>app.UseKotoFastEndpoints()</c> to map endpoints with envelope error responses.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureErrors">Optional error → HTTP status customization, e.g. <c>o =&gt; o.Map("payments.gateway.failed", 502)</c>.</param>
    public static IServiceCollection AddKotoApi(
        this IServiceCollection services,
        Action<KotoApiOptions>? configureErrors = null)
    {
        services.AddKotoAspNetCore(configureErrors);
        services.AddSingleton<CorrelationIdMiddleware>();
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.TryAddSingleton<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Adds <see cref="CorrelationIdMiddleware"/> and the exception handler (500 envelope via
    /// <see cref="GlobalExceptionHandler"/>) to the pipeline. Call this before <c>app.UseKotoFastEndpoints()</c>.
    /// </summary>
    public static IApplicationBuilder UseKotoApi(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = GlobalExceptionHandler.HandleAsync });
        return app;
    }
}
