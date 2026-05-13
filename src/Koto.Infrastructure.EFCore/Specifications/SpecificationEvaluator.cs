using Microsoft.EntityFrameworkCore;

namespace Koto.Infrastructure.EFCore.Specifications;

/// <summary>
/// Applies an <see cref="ISpecification{T}"/> to an <see cref="IQueryable{T}"/>.
/// </summary>
public static class SpecificationEvaluator
{
    /// <summary>
    /// Returns a new queryable with the specification's criteria, includes, and ordering applied.
    /// </summary>
    public static IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> spec)
        where T : class
    {
        if (spec.Criteria is not null)
            query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (q, include) => q.Include(include));

        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            query = query.OrderByDescending(spec.OrderByDescending);

        return query;
    }
}
