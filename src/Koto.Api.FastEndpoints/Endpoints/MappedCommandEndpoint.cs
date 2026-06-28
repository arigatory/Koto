using FastEndpoints;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for result-bearing commands where the HTTP request DTO differs from the dispatched
/// command. Override <see cref="ToCommand"/> to build the command from the bound request <em>and</em>
/// server-side endpoint context (<c>User</c>, <c>Route&lt;T&gt;()</c>, headers) so that server-derived
/// fields (user id, tenant id, …) never appear in the request contract.
/// On success sends 200 OK with <typeparamref name="TResult"/>; on failure sends RFC 7807 Problem Details.
/// </summary>
/// <typeparam name="TRequest">The HTTP request model (what the client sends).</typeparam>
/// <typeparam name="TCommand">The dispatched command type.</typeparam>
/// <typeparam name="TResult">The success response body type.</typeparam>
public abstract class MappedCommandEndpoint<TRequest, TCommand, TResult> : Endpoint<TRequest, TResult>
    where TRequest : notnull
    where TCommand : notnull, Application.ICommand<TResult>
{
    /// <summary>Builds the dispatched command from the bound request and endpoint context.</summary>
    protected abstract TCommand ToCommand(TRequest request);

    /// <inheritdoc/>
    public sealed override Task HandleAsync(TRequest req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<Application.ICqrsDispatcher>().SendAsync<TResult>(ToCommand(req), ct), ct);
}

/// <summary>
/// Base endpoint for void commands where the HTTP request DTO differs from the dispatched command.
/// Override <see cref="ToCommand"/> to build the command from the bound request <em>and</em> server-side
/// endpoint context (<c>User</c>, <c>Route&lt;T&gt;()</c>, headers).
/// On success sends 204 No Content; on failure sends RFC 7807 Problem Details.
/// </summary>
/// <typeparam name="TRequest">The HTTP request model (what the client sends).</typeparam>
/// <typeparam name="TCommand">The dispatched command type.</typeparam>
public abstract class MappedCommandEndpoint<TRequest, TCommand> : Endpoint<TRequest>
    where TRequest : notnull
    where TCommand : notnull, Application.ICommand
{
    /// <summary>Builds the dispatched command from the bound request and endpoint context.</summary>
    protected abstract TCommand ToCommand(TRequest request);

    /// <inheritdoc/>
    public sealed override Task HandleAsync(TRequest req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<Application.ICqrsDispatcher>().SendAsync(ToCommand(req), ct), ct);
}
