using Koto.Domain;

namespace Koto.Application;

/// <summary>Handles a query and returns a read-model result.</summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The type of the result value.</typeparam>
public interface IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>Executes <paramref name="query"/> and returns the result.</summary>
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct = default);
}
