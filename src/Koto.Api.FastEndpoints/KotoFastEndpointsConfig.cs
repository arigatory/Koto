using FastEndpoints;
using FluentValidation.Results;
using Koto.Api.AspNetCore;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Application;
using Koto.Domain;
using Koto.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Koto.Api.FastEndpoints;

/// <summary>Makes FastEndpoints' own error responses (binding and validator failures) use the Koto <see cref="Envelope{T}"/>.</summary>
public static class KotoFastEndpointsConfig
{
    /// <summary>
    /// <c>UseFastEndpoints</c> with the Koto envelope applied to binding and validator failures
    /// (<see cref="ApplyKotoEnvelope"/>); <paramref name="configure"/> runs afterwards for further customization.
    /// </summary>
    public static IApplicationBuilder UseKotoFastEndpoints(this IApplicationBuilder app, Action<Config>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseFastEndpoints(config =>
        {
            config.ApplyKotoEnvelope();
            configure?.Invoke(config);
        });
    }

    /// <summary>
    /// <c>MapFastEndpoints</c> with the Koto envelope applied — for hosts that map endpoints through
    /// <see cref="IEndpointRouteBuilder"/> (e.g. <c>app.UseEndpoints(e =&gt; e.MapKotoFastEndpoints())</c>).
    /// </summary>
    public static IEndpointRouteBuilder MapKotoFastEndpoints(this IEndpointRouteBuilder app, Action<Config>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapFastEndpoints(config =>
        {
            config.ApplyKotoEnvelope();
            configure?.Invoke(config);
        });
    }

    /// <summary>
    /// Configures <see cref="Config.Errors"/> so that binding and validator failures are sent as a
    /// 400 <see cref="Envelope{T}"/> (<c>application/json</c>) instead of FastEndpoints' default error DTO.
    /// </summary>
    public static Config ApplyKotoEnvelope(this Config config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Errors.StatusCode = StatusCodes.Status400BadRequest;
        config.Errors.ContentType = "application/json";
        config.Errors.ProducesMetadataType = typeof(Envelope<object?>);
        config.Errors.ResponseBuilder = BuildErrorEnvelope;
        return config;
    }

    /// <summary>
    /// Maps FastEndpoints validation failures to an envelope: a failure carrying a Koto
    /// <see cref="Error"/> in <see cref="ValidationFailure.CustomState"/> keeps its code, any other rule
    /// becomes <see cref="ValidationRuleFailed"/>; every error is bound to its property.
    /// </summary>
    public static Envelope<object?> BuildErrorEnvelope(List<ValidationFailure> failures, HttpContext httpContext, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(httpContext);
        var errors = failures.Select(ToError).ToArray();
        return Envelope.Fail(errors, httpContext.GetKotoTimeProvider().GetUtcNow(), CorrelationContext.Current.Value);
    }

    private static EnrichedError ToError(ValidationFailure failure)
    {
        var field = string.IsNullOrEmpty(failure.PropertyName) ? null : failure.PropertyName;
        var error = failure.CustomState is Error domainError
            ? domainError.Enrich()
            : new ValidationRuleFailed(
                string.IsNullOrEmpty(failure.ErrorCode) ? "binding" : failure.ErrorCode,
                failure.ErrorMessage).Enrich();
        return error.Field is null && field is not null ? error with { Field = field } : error;
    }
}
