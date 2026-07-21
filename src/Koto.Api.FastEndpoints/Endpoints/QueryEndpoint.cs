using FastEndpoints;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for queries (returns 200 OK with <typeparamref name="TResult"/> on success).
/// Errors with <c>*.not-found</c> code return 404; all other failures return 400 or 500 per
/// <see cref="Koto.Api.AspNetCore.KotoHttpErrorOptions"/>.
/// Subclass and implement <c>Configure</c> — dispatch is wired by the base class.
/// Use <see cref="MappedQueryEndpoint{TRequest,TQuery,TResult}"/> instead when the query carries
/// server-derived parameters that must not be bound from the request.
/// </summary>
/// <typeparam name="TQuery">The query type; also used as the HTTP request model (bound from route/query params).</typeparam>
/// <typeparam name="TResult">The success response body type.</typeparam>
public abstract class QueryEndpoint<TQuery, TResult> : Endpoint<TQuery, TResult>
    where TQuery : notnull, Application.IQuery<TResult>
{
    /// <summary>Dispatches the bound query; subclasses only implement <c>Configure</c>.</summary>
    public sealed override Task HandleAsync(TQuery req, CancellationToken ct) =>
        SendQueryAsync(req, ct);

    /// <summary>
    /// Dispatches <paramref name="query"/> via <see cref="Application.ICqrsDispatcher"/>.
    /// On success sends 200 OK with the result. On failure sends RFC 7807 Problem Details
    /// (404 for <c>*.not-found</c> errors, 400/500 for others).
    /// </summary>
    protected Task SendQueryAsync(TQuery query, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<Application.ICqrsDispatcher>().QueryAsync<TResult>(query, ct), ct);
}
