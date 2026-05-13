using Koto.Api.FastEndpoints.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Api.FastEndpoints;

/// <summary>DI and pipeline registration helpers for <c>Koto.Api.FastEndpoints</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Koto API services: correlation ID middleware, accessor, and global exception handler.
    /// </summary>
    /// <remarks>
    /// Also call <c>app.UseKotoApi()</c> to add the middleware to the pipeline, and
    /// <c>app.UseFastEndpoints()</c> to register FastEndpoints routes.
    /// </remarks>
    public static IServiceCollection AddKotoApi(this IServiceCollection services)
    {
        services.AddSingleton<CorrelationIdMiddleware>();
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    /// Adds <see cref="CorrelationIdMiddleware"/> and <c>UseExceptionHandler</c> to the pipeline.
    /// Call this before <c>app.UseFastEndpoints()</c>.
    /// </summary>
    public static IApplicationBuilder UseKotoApi(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        return app;
    }
}
