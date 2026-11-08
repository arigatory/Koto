using Koto.Application;
using Koto.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Maps <see cref="AppResult{T}"/> to MVC action results: the Koto <see cref="Envelope{T}"/> with 200 on
/// success, or the status chosen by <see cref="KotoApiOptions"/> on failure.
/// </summary>
public static class ResultActionResultExtensions
{
    /// <summary>Maps a result to an envelope: 200 with <c>Envelope&lt;T&gt;</c> on success; classified status with <c>Envelope&lt;object?&gt;</c> on failure.</summary>
    public static ActionResult<Envelope<T>> ToActionResult<T>(this AppResult<T> result, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        var httpContext = controller.HttpContext;
        var now = httpContext.GetKotoTimeProvider().GetUtcNow();
        return result.IsSuccess
            ? Envelope.Ok(result.Value, now, correlationId)
            : new ObjectResult(Envelope.Fail(result.Errors, now, correlationId)) { StatusCode = result.ResolveStatusCode(httpContext) };
    }

    /// <summary>Maps a void result to an envelope whose result is <c>null</c>.</summary>
    public static ActionResult<Envelope<object?>> ToActionResult(this AppResult<Unit> result, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        var httpContext = controller.HttpContext;
        var envelope = result.ToEnvelope(httpContext.GetKotoTimeProvider().GetUtcNow(), correlationId);
        return result.IsSuccess
            ? envelope
            : new ObjectResult(envelope) { StatusCode = result.ResolveStatusCode(httpContext) };
    }

    /// <summary>Awaits a dispatch and maps the result — <c>(await dispatcher.SendAsync(cmd, ct)).ToActionResult(this)</c>.</summary>
    public static async Task<ActionResult<Envelope<T>>> ToActionResultAsync<T>(
        this Task<AppResult<T>> pending, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToActionResult(controller, correlationId);
    }

    /// <summary>Awaits a void dispatch and maps the result.</summary>
    public static async Task<ActionResult<Envelope<object?>>> ToActionResultAsync(
        this Task<AppResult<Unit>> pending, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToActionResult(controller, correlationId);
    }
}
