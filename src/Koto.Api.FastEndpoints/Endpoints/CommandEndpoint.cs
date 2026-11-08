using FastEndpoints;
using App = Koto.Application;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for void commands. The command type is the request model; the response is the Koto
/// <see cref="App.Envelope{T}"/> with a <c>null</c> result (200 on success, classified status on failure).
/// Subclasses only implement <c>Configure</c>. Use <see cref="MappedCommandEndpoint{TRequest,TCommand}"/>
/// when the command carries server-derived fields that must not be bound from the request.
/// </summary>
/// <typeparam name="TCommand">The command type; also used as the HTTP request model.</typeparam>
public abstract class CommandEndpoint<TCommand> : Endpoint<TCommand, App.Envelope<object?>>
    where TCommand : notnull, App.ICommand
{
    /// <summary>Dispatches the bound command; subclasses only implement <c>Configure</c>.</summary>
    public sealed override Task HandleAsync(TCommand req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<App.ICqrsDispatcher>().SendAsync(req, ct), ct);
}

/// <summary>
/// Base endpoint for commands that return a result: responds with <see cref="App.Envelope{T}"/> of
/// <typeparamref name="TResult"/> (200 on success, classified status on failure).
/// Subclasses only implement <c>Configure</c>. Use <see cref="MappedCommandEndpoint{TRequest,TCommand,TResult}"/>
/// when the command carries server-derived fields that must not be bound from the request.
/// </summary>
/// <typeparam name="TCommand">The command type; also used as the HTTP request model.</typeparam>
/// <typeparam name="TResult">The success result type inside the envelope.</typeparam>
public abstract class CommandEndpoint<TCommand, TResult> : Endpoint<TCommand, App.Envelope<TResult>>
    where TCommand : notnull, App.ICommand<TResult>
{
    /// <summary>Dispatches the bound command; subclasses only implement <c>Configure</c>.</summary>
    public sealed override Task HandleAsync(TCommand req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<App.ICqrsDispatcher>().SendAsync<TResult>(req, ct), ct);
}
