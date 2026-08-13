using Microsoft.EntityFrameworkCore;

namespace Agendio.Infrastructure.Persistence;

public sealed record PagedItems<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public static class PaginationExtensions
{
    public const int MaxPageSize = 100;

    /// <summary>
    /// Normaliza page/pageSize, conta o total e materializa a pagina — mesma
    /// logica antes duplicada em cada ListXxxQueryHandler. Aplicar depois de
    /// Where/OrderBy (e de Select, se a projecao for a fundida na query).
    /// </summary>
    public static async Task<PagedItems<T>> ToPagedItemsAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedItems<T>(items, totalCount, normalizedPage, normalizedPageSize);
    }
}
