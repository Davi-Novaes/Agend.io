using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.GetInventorySummary;

public sealed class GetInventorySummaryQueryHandler(EstoqueDbContext dbContext) : IQueryHandler<GetInventorySummaryQuery, InventorySummary>
{
    public async Task<Result<InventorySummary>> Handle(GetInventorySummaryQuery request, CancellationToken cancellationToken)
    {
        var products = await dbContext.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.QuantityInStock,
                p.MinimumStock,
                Amount = p.SalePrice == null ? (decimal?)null : p.SalePrice.Amount,
                Currency = p.SalePrice == null ? null : p.SalePrice.Currency,
            })
            .ToListAsync(cancellationToken);

        var lowStockCount = products.Count(p => p.QuantityInStock <= p.MinimumStock);

        var totalStockValue = products
            .Where(p => p.Amount is not null)
            .GroupBy(p => p.Currency!)
            .Select(group => new StockValueByCurrency(group.Key, group.Sum(p => p.QuantityInStock * p.Amount!.Value)))
            .OrderByDescending(point => point.Total)
            .ToList();

        return Result.Success(new InventorySummary(products.Count, lowStockCount, totalStockValue));
    }
}
