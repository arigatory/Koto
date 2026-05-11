namespace Koto.Application;

/// <summary>Marker interface for queries that return <see cref="Koto.Domain.Result{TResult}"/>.</summary>
/// <typeparam name="TResult">The type of the query result.</typeparam>
public interface IQuery<TResult> { }
