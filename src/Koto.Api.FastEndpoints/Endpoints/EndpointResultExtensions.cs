using FastEndpoints;
using Koto.Api.AspNetCore;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Application;
using Koto.Domain;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Shared mapping from a dispatched <see cref="AppResult{T}"/> to the HTTP response: the Koto
/// <see cref="Envelope{T}"/> with 200 on success, or the status chosen by <see cref="KotoApiOptions"/>
/// on failure (all errors preserved, correlation id attached).
/// </summary>
internal static class EndpointResultExtensions
{
    /// <summary>Awaits a void-command dispatch and sends an envelope whose result is <c>null</c>.</summary>
    public static async Task SendDispatchAsync(this IEndpoint endpoint, Task<AppResult<Unit>> pending, CancellationToken ct)
    {
        var result = await pending.ConfigureAwait(false);
        var httpContext = endpoint.HttpContext;
        var envelope = result.ToEnvelope(httpContext.GetKotoTimeProvider().GetUtcNow(), CorrelationContext.Current.Value);
        await httpContext.Response.SendAsync(envelope, result.ResolveStatusCode(httpContext), cancellation: ct).ConfigureAwait(false);
    }

    /// <summary>Awaits a result-bearing dispatch and sends the typed envelope.</summary>
    public static async Task SendDispatchAsync<TResult>(this IEndpoint endpoint, Task<AppResult<TResult>> pending, CancellationToken ct)
    {
        var result = await pending.ConfigureAwait(false);
        var httpContext = endpoint.HttpContext;
        var envelope = result.ToEnvelope(httpContext.GetKotoTimeProvider().GetUtcNow(), CorrelationContext.Current.Value);
        await httpContext.Response.SendAsync(envelope, result.ResolveStatusCode(httpContext), cancellation: ct).ConfigureAwait(false);
    }
}
