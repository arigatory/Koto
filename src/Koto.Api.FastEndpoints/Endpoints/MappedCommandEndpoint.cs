using FastEndpoints;
using App = Koto.Application;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for result-bearing commands where the HTTP request DTO differs from the dispatched
/// command. Override <see cref="ToCommand"/> to build the command from the bound request <em>and</em>
/// server-side endpoint context (<c>User</c>, <c>Route&lt;T&gt;()</c>, headers) so that server-derived
/// fields (user id, tenant id, …) never appear in the request contract.
/// Responds with <see cref="App.Envelope{T}"/> of <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TRequest">The HTTP request model (what the client sends).</typeparam>
/// <typeparam name="TCommand">The dispatched command type.</typeparam>
/// <typeparam name="TResult">The success result type inside the envelope.</typeparam>
public abstract class MappedCommandEndpoint<TRequest, TCommand, TResult> : Endpoint<TRequest, App.Envelope<TResult>>
    where TRequest : notnull
    where TCommand : notnull, App.ICommand<TResult>
{
    /// <summary>Builds the dispatched command from the bound request and endpoint context.</summary>
    protected abstract TCommand ToCommand(TRequest request);

    /// <inheritdoc/>
    public sealed override Task HandleAsync(TRequest req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<App.ICqrsDispatcher>().SendAsync<TResult>(ToCommand(req), ct), ct);
}

/// <summary>
/// Base endpoint for void commands where the HTTP request DTO differs from the dispatched command.
/// Override <see cref="ToCommand"/> to build the command from the bound request <em>and</em> server-side
/// endpoint context (<c>User</c>, <c>Route&lt;T&gt;()</c>, headers).
/// Responds with <see cref="App.Envelope{T}"/> whose result is <c>null</c>.
/// </summary>
/// <typeparam name="TRequest">The HTTP request model (what the client sends).</typeparam>
/// <typeparam name="TCommand">The dispatched command type.</typeparam>
public abstract class MappedCommandEndpoint<TRequest, TCommand> : Endpoint<TRequest, App.Envelope<object?>>
    where TRequest : notnull
    where TCommand : notnull, App.ICommand
{
    /// <summary>Builds the dispatched command from the bound request and endpoint context.</summary>
    protected abstract TCommand ToCommand(TRequest request);

    /// <inheritdoc/>
    public sealed override Task HandleAsync(TRequest req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<App.ICqrsDispatcher>().SendAsync(ToCommand(req), ct), ct);
}
