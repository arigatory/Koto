using FastEndpoints;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Api.FastEndpoints.ProblemDetails;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for void commands (returns 204 No Content on success).
/// Subclass, implement <c>HandleAsync</c> and call <see cref="SendCommandAsync"/>.
/// </summary>
/// <typeparam name="TCommand">The command type; also used as the HTTP request model.</typeparam>
public abstract class CommandEndpoint<TCommand> : Endpoint<TCommand>
    where TCommand : notnull, Application.ICommand
{
    /// <summary>
    /// Dispatches <paramref name="command"/> via <see cref="Application.ICqrsDispatcher"/>.
    /// On success sends 204 No Content. On failure sends RFC 7807 Problem Details.
    /// </summary>
    protected async Task SendCommandAsync(TCommand command, CancellationToken ct)
    {
        var result = await Resolve<Application.ICqrsDispatcher>().SendAsync(command, ct);
        if (result.IsFailure)
            await HttpContext.Response.SendResultAsync(
                KotoProblemDetails.From(result.Error, CorrelationContext.Current.Value));
        else
            await HttpContext.Response.SendNoContentAsync(ct);
    }
}

/// <summary>
/// Base endpoint for commands that return a result (returns 200 OK with <typeparamref name="TResult"/> on success).
/// Subclass, implement <c>HandleAsync</c> and call <see cref="SendCommandAsync"/>.
/// </summary>
/// <typeparam name="TCommand">The command type; also used as the HTTP request model.</typeparam>
/// <typeparam name="TResult">The success response body type.</typeparam>
public abstract class CommandEndpoint<TCommand, TResult> : Endpoint<TCommand, TResult>
    where TCommand : notnull, Application.ICommand<TResult>
{
    /// <summary>
    /// Dispatches <paramref name="command"/> via <see cref="Application.ICqrsDispatcher"/>.
    /// On success sends 200 OK with the result. On failure sends RFC 7807 Problem Details.
    /// </summary>
    protected async Task SendCommandAsync(TCommand command, CancellationToken ct)
    {
        var result = await Resolve<Application.ICqrsDispatcher>().SendAsync<TResult>(command, ct);
        if (result.IsFailure)
            await HttpContext.Response.SendResultAsync(
                KotoProblemDetails.From(result.Error, CorrelationContext.Current.Value));
        else
            await HttpContext.Response.SendOkAsync(result.Value, cancellation: ct);
    }
}
