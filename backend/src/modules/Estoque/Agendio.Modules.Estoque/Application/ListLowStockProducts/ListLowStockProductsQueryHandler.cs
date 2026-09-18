using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.ListLowStockProducts;

// Mesmo criterio de GetInventorySummaryQueryHandler.LowStockCount (QuantityInStock
// <= MinimumStock, produto ativo) -- pra contagem e lista nunca divergirem.
public sealed class ListLowStockProductsQueryHandler(EstoqueDbContext dbContext)
    : IQueryHandler<ListLowStockProductsQuery, IReadOnlyList<LowStockProduct>>
{
    public async Task<Result<IReadOnlyList<LowStockProduct>>> Handle(ListLowStockProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await dbContext.Products.AsNoTracking()
            .Where(p => p.IsActive && p.QuantityInStock <= p.MinimumStock)
            .OrderBy(p => p.QuantityInStock)
            .Select(p => new LowStockProduct(p.Id.Value, p.Name, p.QuantityInStock, p.MinimumStock))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LowStockProduct>>(products);
    }
}
