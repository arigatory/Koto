using Koto.Application;
using Microsoft.EntityFrameworkCore;

namespace Koto.Infrastructure.EFCore;

/// <summary>Материализация <see cref="PagedList{T}"/> из EF Core-запросов.</summary>
public static class QueryablePagingExtensions
{
    /// <summary>
    /// Выполняет COUNT + Skip/Take и возвращает страницу.
    /// Запрос должен быть упорядочен (<c>OrderBy</c>) — иначе порядок страниц не определён.
    /// </summary>
    /// <param name="query">Источник (уже с фильтрами и сортировкой).</param>
    /// <param name="page">Номер страницы, с 1.</param>
    /// <param name="pageSize">Размер страницы (&gt;= 1).</param>
    /// <param name="ct">Токен отмены.</param>
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        if (totalCount == 0)
            return PagedList<T>.Empty(page, pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedList<T>(items, page, pageSize, totalCount);
    }
}
