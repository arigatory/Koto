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
public static partial class KotoFastEndpointsConfig
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
            config.AllowParameterlessRequests();
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
            config.AllowParameterlessRequests();
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
    /// Lets a command or query without properties (<c>record GetHealthQuery : IQuery&lt;HealthDto&gt;</c>,
    /// <c>record RebuildIndexCommand : ICommand</c>) be the request model of a Koto endpoint. FastEndpoints
    /// rejects such DTOs by default, and it does so on the first request rather than at startup, so the
    /// endpoint answered 500 before the handler ran. Sets <see cref="EndpointOptions.AllowEmptyRequestDtos"/> and
    /// drops the default <c>application/json</c> requirement for endpoints with nothing to read from the body — a request
    /// without properties, or one whose properties all come from the route — so <c>POST /reset</c> and
    /// <c>POST /signals/{signalId}/approve</c> work without a body.
    /// The second part goes through <see cref="EndpointOptions.Configurator"/> — see <see cref="ApplyKotoDefaults"/>
    /// if the host sets a configurator of its own.
    /// </summary>
    public static Config AllowParameterlessRequests(this Config config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Endpoints.AllowEmptyRequestDtos = true;

        config.Endpoints.Configurator = ApplyKotoDefaults;
        return config;
    }

    /// <summary>
    /// The per-endpoint part of <see cref="AllowParameterlessRequests"/>. <see cref="EndpointOptions.Configurator"/>
    /// holds a single action, so a host that assigns its own replaces Koto's — call this from that action
    /// (<c>c.Endpoints.Configurator = ep =&gt; { KotoFastEndpointsConfig.ApplyKotoDefaults(ep); ... }</c>) to keep both.
    /// </summary>
    public static void ApplyKotoDefaults(EndpointDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (HasNothingToReadFromBody(definition))
            definition.Description(builder => builder.ClearDefaultAccepts());
    }

    // No body is expected when the request model has no bindable properties at all, or when every one of
    // them is filled from the route (POST /signals/{signalId}/approve with record ApproveSignalCommand(Guid SignalId)).
    // Such an endpoint must not demand "Content-Type: application/json".
    private static bool HasNothingToReadFromBody(EndpointDefinition definition)
    {
        if (definition.ReqDtoType == typeof(EmptyRequest))
            return false;

        var properties = definition.ReqDtoType
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToArray();
        if (properties.Length == 0)
            return true;

        var routes = definition.Routes;
        return routes is { Length: > 0 }
            && routes.All(route =>
            {
                var parameters = RouteParameterNames(route);
                return properties.All(parameters.Contains);
            });
    }

    private static HashSet<string> RouteParameterNames(string route)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in RouteParameter().Matches(route))
            names.Add(match.Groups[1].Value);
        return names;
    }

    // {id}, {id:guid}, {id?}, {*path}, {id=5} → "id" / "path"
    [System.Text.RegularExpressions.GeneratedRegex(@"\{\*{0,2}([A-Za-z_][A-Za-z0-9_]*)[^}]*\}")]
    private static partial System.Text.RegularExpressions.Regex RouteParameter();

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
