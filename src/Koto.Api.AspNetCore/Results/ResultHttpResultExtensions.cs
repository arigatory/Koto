using Koto.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Maps <see cref="Result{T}"/> to Minimal API <see cref="IResult"/> responses:
/// 200 OK (or 204 No Content for <see cref="Unit"/>) on success, RFC 7807 Problem Details
/// on failure. Status codes come from the <see cref="KotoHttpErrorOptions"/> registered
/// via <c>AddKotoAspNetCore()</c> (built-in defaults are used when not registered).
/// </summary>
public static class ResultHttpResultExtensions
{
    /// <summary>Maps a result to 200 OK with the value, or Problem Details on failure.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : KotoProblemDetails.From(result.Errors, httpContext.GetKotoHttpErrorOptions(), correlationId);
    }

    /// <summary>Maps a void result to 204 No Content, or Problem Details on failure.</summary>
    public static IResult ToHttpResult(this Result<Unit> result, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        return result.IsSuccess
            ? TypedResults.NoContent()
            : KotoProblemDetails.From(result.Errors, httpContext.GetKotoHttpErrorOptions(), correlationId);
    }

    /// <summary>Awaits a dispatch and maps the result — <c>dispatcher.SendAsync(cmd, ct).ToHttpResultAsync(ctx)</c>.</summary>
    public static async Task<IResult> ToHttpResultAsync<T>(
        this Task<Result<T>> pending, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToHttpResult(httpContext, correlationId);
    }

    /// <summary>Awaits a void dispatch and maps the result to 204 No Content or Problem Details.</summary>
    public static async Task<IResult> ToHttpResultAsync(
        this Task<Result<Unit>> pending, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToHttpResult(httpContext, correlationId);
    }

    /// <summary>
    /// Resolves the <see cref="KotoHttpErrorOptions"/> registered via <c>AddKotoAspNetCore()</c>,
    /// or the built-in defaults when none are registered.
    /// </summary>
    public static KotoHttpErrorOptions GetKotoHttpErrorOptions(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.RequestServices.GetService<IOptions<KotoHttpErrorOptions>>()?.Value
            ?? new KotoHttpErrorOptions();
    }
}
