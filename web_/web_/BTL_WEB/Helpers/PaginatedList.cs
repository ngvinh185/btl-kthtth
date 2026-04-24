using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Helpers;

public class PaginatedList<T>
{
    public IReadOnlyList<T> Items { get; private set; } = [];

    public int PageIndex { get; private set; }

    public int TotalPages { get; private set; }

    public int TotalItems { get; private set; }

    public int PageSize { get; private set; }

    public bool HasPreviousPage => PageIndex > 1;

    public bool HasNextPage => PageIndex < TotalPages;

    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
    {
        var totalItems = await source.CountAsync();
        var items = await source
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<T>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize))
        };
    }
}
