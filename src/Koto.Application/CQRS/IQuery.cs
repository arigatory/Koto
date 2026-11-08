namespace Koto.Application;

/// <summary>Marker interface for queries; handlers return <see cref="AppResult{TResult}"/>.</summary>
/// <typeparam name="TResult">The type of the query result.</typeparam>
public interface IQuery<TResult> : IQueryBase { }
