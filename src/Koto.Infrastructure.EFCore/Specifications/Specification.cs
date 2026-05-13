using System.Linq.Expressions;

namespace Koto.Infrastructure.EFCore.Specifications;

/// <summary>
/// Base class for building specifications. Override the constructor to call
/// <see cref="AddCriteria"/>, <see cref="AddInclude"/>, and <see cref="ApplyOrderBy"/>.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    /// <inheritdoc/>
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    /// <inheritdoc/>
    public List<Expression<Func<T, object>>> Includes { get; } = [];

    /// <inheritdoc/>
    public Expression<Func<T, object>>? OrderBy { get; private set; }

    /// <inheritdoc/>
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    /// <summary>Sets the WHERE filter.</summary>
    protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    /// <summary>Adds an eager-load navigation.</summary>
    protected void AddInclude(Expression<Func<T, object>> include) => Includes.Add(include);

    /// <summary>Sets ascending ordering.</summary>
    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    /// <summary>Sets descending ordering.</summary>
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) =>
        OrderByDescending = orderByDescending;
}
