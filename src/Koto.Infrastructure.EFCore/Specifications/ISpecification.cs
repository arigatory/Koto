using System.Linq.Expressions;

namespace Koto.Infrastructure.EFCore.Specifications;

/// <summary>
/// Encapsulates a query as an object: filter criteria, includes, and ordering.
/// </summary>
public interface ISpecification<T>
{
    /// <summary>Filter predicate applied as a <c>WHERE</c> clause.</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>Navigation properties to eager-load.</summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>Ascending order expression.</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>Descending order expression.</summary>
    Expression<Func<T, object>>? OrderByDescending { get; }
}
