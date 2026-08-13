using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.ListProducts;

public sealed class ListProductsQueryHandler(EstoqueDbContext dbContext) : IQueryHandler<ListProductsQuery, ListProductsResult>
{
    public async Task<Result<ListProductsResult>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
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

        var paged = await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummary(
                p.Id.Value, p.Name, p.Sku, p.QuantityInStock, p.MinimumStock,
                p.SalePrice == null ? null : p.SalePrice.Amount,
                p.SalePrice == null ? null : p.SalePrice.Currency,
                p.IsActive, p.QuantityInStock <= p.MinimumStock))
            .ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        return Result.Success(new ListProductsResult(paged.Items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
