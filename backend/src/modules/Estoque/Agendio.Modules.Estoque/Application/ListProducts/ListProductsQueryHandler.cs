using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.ListProducts;

public sealed class ListProductsQueryHandler(EstoqueDbContext dbContext) : IQueryHandler<ListProductsQuery, ListProductsResult>
{
    public async Task<Result<ListProductsResult>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search}%"));
        }

        if (request.IsActive is not null)
        {
            query = query.Where(p => p.IsActive == request.IsActive);
        }

        if (request.LowStockOnly)
        {
            query = query.Where(p => p.QuantityInStock <= p.MinimumStock);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductSummary(
                p.Id.Value, p.Name, p.Sku, p.QuantityInStock, p.MinimumStock,
                p.SalePrice == null ? null : p.SalePrice.Amount,
                p.SalePrice == null ? null : p.SalePrice.Currency,
                p.IsActive, p.QuantityInStock <= p.MinimumStock))
            .ToListAsync(cancellationToken);

        return Result.Success(new ListProductsResult(items, totalCount, page, pageSize));
    }
}
