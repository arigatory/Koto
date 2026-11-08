using FastEndpoints;
using App = Koto.Application;

namespace Koto.Api.FastEndpoints.Endpoints;

/// <summary>
/// Base endpoint for queries: responds with <see cref="App.Envelope{T}"/> of <typeparamref name="TResult"/>
/// (200 on success; 404 for <c>*.not-found</c> and other classified statuses on failure).
/// Subclasses only implement <c>Configure</c>. Use <see cref="MappedQueryEndpoint{TRequest,TQuery,TResult}"/>
/// when the query carries server-derived parameters that must not be bound from the request.
/// </summary>
/// <typeparam name="TQuery">The query type; also used as the HTTP request model (bound from route/query params).</typeparam>
/// <typeparam name="TResult">The success result type inside the envelope.</typeparam>
public abstract class QueryEndpoint<TQuery, TResult> : Endpoint<TQuery, App.Envelope<TResult>>
    where TQuery : notnull, App.IQuery<TResult>
{
    /// <summary>Dispatches the bound query; subclasses only implement <c>Configure</c>.</summary>
    public sealed override Task HandleAsync(TQuery req, CancellationToken ct) =>
        this.SendDispatchAsync(Resolve<App.ICqrsDispatcher>().QueryAsync<TResult>(req, ct), ct);
}
