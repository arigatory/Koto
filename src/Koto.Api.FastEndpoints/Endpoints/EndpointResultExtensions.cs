using FastEndpoints;
using Koto.Api.AspNetCore;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Shared mapping from a dispatched <see cref="Result{T}"/> to an HTTP response:
/// success sends 200/204, failure sends RFC 7807 Problem Details via
/// <see cref="KotoProblemDetails"/> (all errors preserved; status from <see cref="KotoHttpErrorOptions"/>).
/// </summary>
internal static class EndpointResultExtensions
{
    /// <summary>Awaits a void-command dispatch and sends 204 No Content on success, Problem Details on failure.</summary>
    public static async Task SendDispatchAsync(
        this IEndpoint endpoint, Task<Result<Unit>> pending, CancellationToken ct)
    {
        var result = await pending.ConfigureAwait(false);
        if (result.IsFailure)
            await endpoint.HttpContext.Response.SendResultAsync(ToProblem(endpoint, result.Errors)).ConfigureAwait(false);
        else
            await endpoint.HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Awaits a result-bearing dispatch and sends 200 OK with the value on success, Problem Details on failure.</summary>
    public static async Task SendDispatchAsync<TResult>(
        this IEndpoint endpoint, Task<Result<TResult>> pending, CancellationToken ct)
    {
        var result = await pending.ConfigureAwait(false);
        if (result.IsFailure)
            await endpoint.HttpContext.Response.SendResultAsync(ToProblem(endpoint, result.Errors)).ConfigureAwait(false);
        else
            await endpoint.HttpContext.Response.SendOkAsync(result.Value, cancellation: ct).ConfigureAwait(false);
    }

    private static IResult ToProblem(IEndpoint endpoint, IReadOnlyList<Error> errors) =>
        KotoProblemDetails.From(
            errors,
            endpoint.HttpContext.GetKotoHttpErrorOptions(),
            CorrelationContext.Current.Value);
}
