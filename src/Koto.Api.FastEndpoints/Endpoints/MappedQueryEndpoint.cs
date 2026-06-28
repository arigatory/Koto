using FastEndpoints;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for queries where the HTTP request DTO differs from the dispatched query.
/// Override <see cref="ToQuery"/> to build the query from the bound request <em>and</em> server-side
/// endpoint context (<c>User</c>, <c>Route&lt;T&gt;()</c>, headers) — e.g. a "get my …" query whose owner
/// id comes from the caller's claims rather than the request.
/// On success sends 200 OK with <typeparamref name="TResult"/>; on failure sends RFC 7807 Problem Details
/// (404 for <c>*.not-found</c>, 400/500 otherwise).
/// </summary>
/// <typeparam name="TRequest">The HTTP request model (what the client sends).</typeparam>
/// <typeparam name="TQuery">The dispatched query type.</typeparam>
/// <typeparam name="TResult">The success response body type.</typeparam>
public abstract class MappedQueryEndpoint<TRequest, TQuery, TResult> : Endpoint<TRequest, TResult>
    where TRequest : notnull
    where TQuery : notnull, Application.IQuery<TResult>
{
    /// <summary>Builds the dispatched query from the bound request and endpoint context.</summary>
    protected abstract TQuery ToQuery(TRequest request);

    /// <inheritdoc/>
    public sealed override Task HandleAsync(TRequest req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<Application.ICqrsDispatcher>().QueryAsync<TResult>(ToQuery(req), ct), ct);
}
