// PURPOSE: copied verbatim from the real ag-kit Ag.Abstractions - turns a
// filtered+ordered IQueryable into a PagedResult (COUNT query, then a
// Skip/Take query). Used by the real List query handlers.
namespace Ag.Abstractions.Extensions;

using Ag.Abstractions.Common;
using Microsoft.EntityFrameworkCore;

public static class PaginationExtension
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }
}
