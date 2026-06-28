using FastEndpoints;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Api.FastEndpoints.ProblemDetails;
using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Shared mapping from a dispatched <see cref="Result{T}"/> to an HTTP response:
/// success sends 200/204, failure sends RFC 7807 Problem Details via <see cref="KotoProblemDetails"/>.
/// </summary>
internal static class EndpointResultExtensions
{
    /// <summary>Awaits a void-command dispatch and sends 204 No Content on success, Problem Details on failure.</summary>
    public static async Task SendDispatchAsync(
        this IEndpoint endpoint, Task<Result<Unit>> pending, CancellationToken ct)
    {
        var result = await pending;
        if (result.IsFailure)
            await endpoint.HttpContext.Response.SendResultAsync(
                KotoProblemDetails.From(result.Error, CorrelationContext.Current.Value));
        else
            await endpoint.HttpContext.Response.SendNoContentAsync(ct);
    }

    /// <summary>Awaits a result-bearing dispatch and sends 200 OK with the value on success, Problem Details on failure.</summary>
    public static async Task SendDispatchAsync<TResult>(
        this IEndpoint endpoint, Task<Result<TResult>> pending, CancellationToken ct)
    {
        var result = await pending;
        if (result.IsFailure)
            await endpoint.HttpContext.Response.SendResultAsync(
                KotoProblemDetails.From(result.Error, CorrelationContext.Current.Value));
        else
            await endpoint.HttpContext.Response.SendOkAsync(result.Value, cancellation: ct);
    }
}
