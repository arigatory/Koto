using FastEndpoints;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for void commands (returns 204 No Content on success).
/// Subclass and implement <c>Configure</c> — dispatch is wired by the base class.
/// Use <see cref="MappedCommandEndpoint{TRequest,TCommand}"/> instead when the command carries
/// server-derived fields that must not be bound from the request.
/// </summary>
/// <typeparam name="TCommand">The command type; also used as the HTTP request model.</typeparam>
public abstract class CommandEndpoint<TCommand> : Endpoint<TCommand>
    where TCommand : notnull, Application.ICommand
{
    /// <summary>Dispatches the bound command; subclasses only implement <c>Configure</c>.</summary>
    public sealed override Task HandleAsync(TCommand req, CancellationToken ct) =>
        SendCommandAsync(req, ct);

    /// <summary>
    /// Dispatches <paramref name="command"/> via <see cref="Application.ICqrsDispatcher"/>.
    /// On success sends 204 No Content. On failure sends RFC 7807 Problem Details.
    /// </summary>
    protected Task SendCommandAsync(TCommand command, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<Application.ICqrsDispatcher>().SendAsync(command, ct), ct);
}

/// <summary>
/// Base endpoint for commands that return a result (returns 200 OK with <typeparamref name="TResult"/> on success).
/// Subclass and implement <c>Configure</c> — dispatch is wired by the base class.
/// Use <see cref="MappedCommandEndpoint{TRequest,TCommand,TResult}"/> instead when the command carries
/// server-derived fields that must not be bound from the request.
/// </summary>
/// <typeparam name="TCommand">The command type; also used as the HTTP request model.</typeparam>
/// <typeparam name="TResult">The success response body type.</typeparam>
public abstract class CommandEndpoint<TCommand, TResult> : Endpoint<TCommand, TResult>
    where TCommand : notnull, Application.ICommand<TResult>
{
    /// <summary>Dispatches the bound command; subclasses only implement <c>Configure</c>.</summary>
    public sealed override Task HandleAsync(TCommand req, CancellationToken ct) =>
        SendCommandAsync(req, ct);

    /// <summary>
    /// Dispatches <paramref name="command"/> via <see cref="Application.ICqrsDispatcher"/>.
    /// On success sends 200 OK with the result. On failure sends RFC 7807 Problem Details.
    /// </summary>
    protected Task SendCommandAsync(TCommand command, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<Application.ICqrsDispatcher>().SendAsync<TResult>(command, ct), ct);
}
