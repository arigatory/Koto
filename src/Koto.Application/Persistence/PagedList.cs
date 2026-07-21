namespace Koto.Application;

/// <summary>
/// Страница результата запроса с метаданными пагинации.
/// Возвращайте из query-хендлеров: <c>IQuery&lt;PagedList&lt;OrderDto&gt;&gt;</c>.
/// Материализация из EF Core — <c>ToPagedListAsync</c> в <c>Koto.Infrastructure.EFCore</c>.
/// </summary>
/// <typeparam name="T">Тип элемента страницы.</typeparam>
public sealed class PagedList<T>
{
    /// <summary>Элементы текущей страницы.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Номер страницы (нумерация с 1).</summary>
    public int Page { get; }

    /// <summary>Размер страницы, запрошенный вызывающим.</summary>
    public int PageSize { get; }

    /// <summary>Общее число элементов во всех страницах.</summary>
    public int TotalCount { get; }

    /// <summary>Общее число страниц.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>Есть ли страница после текущей.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Есть ли страница до текущей.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Создаёт страницу.</summary>
    /// <exception cref="ArgumentOutOfRangeException">page &lt; 1, pageSize &lt; 1 или totalCount &lt; 0.</exception>
    public PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>Пустая страница.</summary>
    public static PagedList<T> Empty(int page = 1, int pageSize = 20) =>
        new([], page, pageSize, 0);

    /// <summary>Проецирует элементы страницы, сохраняя метаданные пагинации.</summary>
    public PagedList<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new PagedList<TResult>([.. Items.Select(selector)], Page, PageSize, TotalCount);
    }
}
