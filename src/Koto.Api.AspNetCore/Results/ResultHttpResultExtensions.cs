using Koto.Application;
using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Maps <see cref="AppResult{T}"/> to Minimal API <see cref="IResult"/>s: the Koto <see cref="Envelope{T}"/>
/// with 200 on success, or the status chosen by <see cref="KotoApiOptions"/> on failure. Only application
/// results are accepted — a domain <see cref="Result{T}"/> must be enriched first.
/// </summary>
public static class ResultHttpResultExtensions
{
    /// <summary>Maps a result to an envelope: 200 on success, classified status on failure.</summary>
    public static IResult ToHttpResult<T>(this AppResult<T> result, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        var envelope = result.ToEnvelope(httpContext.GetKotoTimeProvider().GetUtcNow(), correlationId);
        return TypedResults.Json(envelope, statusCode: result.ResolveStatusCode(httpContext));
    }

    /// <summary>Maps a void result to an envelope whose result is <c>null</c>.</summary>
    public static IResult ToHttpResult(this AppResult<Unit> result, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        var envelope = result.ToEnvelope(httpContext.GetKotoTimeProvider().GetUtcNow(), correlationId);
        return TypedResults.Json(envelope, statusCode: result.ResolveStatusCode(httpContext));
    }

    /// <summary>Awaits a dispatch and maps the result — <c>dispatcher.SendAsync(cmd, ct).ToHttpResultAsync(ctx)</c>.</summary>
    public static async Task<IResult> ToHttpResultAsync<T>(
        this Task<AppResult<T>> pending, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToHttpResult(httpContext, correlationId);
    }

    /// <summary>Awaits a void dispatch and maps the result.</summary>
    public static async Task<IResult> ToHttpResultAsync(
        this Task<AppResult<Unit>> pending, HttpContext httpContext, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToHttpResult(httpContext, correlationId);
    }

    /// <summary>
    /// The HTTP status for <paramref name="result"/>: 200 on success; on failure the
    /// <see cref="EnrichmentGuard"/> runs and <see cref="KotoApiOptions"/> classifies the first error.
    /// </summary>
    public static int ResolveStatusCode<T>(this AppResult<T> result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        if (result.IsSuccess)
            return StatusCodes.Status200OK;
        httpContext.GetEnrichmentGuard()?.Check(result.Errors);
        return httpContext.GetKotoApiOptions().StatusCodeFor(result.FirstError);
    }
}
