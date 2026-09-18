using Agendio.Modules.Estoque.Application.GetInventorySummary;
using Agendio.Modules.Estoque.Application.ListLowStockProducts;
using Agendio.Modules.Estoque.Contracts;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Infrastructure;

// Delega para os handlers ja existentes via IDispatcher (chamada intra-modulo,
// permitida) em vez de duplicar a logica de agregacao aqui.
public sealed class InventorySummaryLookupService(IDispatcher dispatcher) : IInventorySummaryLookupService
{
    public async Task<InventorySummaryLookupResult> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.Query(new GetInventorySummaryQuery(), cancellationToken);
        var summary = result.Value;

        return new InventorySummaryLookupResult(
            summary.ActiveProductCount,
            summary.LowStockCount,
            summary.TotalStockValue.Select(v => new StockValueByCurrencyLookup(v.Currency, v.Total)).ToList());
    }

    public async Task<IReadOnlyList<LowStockProductLookupResult>> ListLowStockAsync(CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.Query(new ListLowStockProductsQuery(), cancellationToken);
        return result.Value
            .Select(p => new LowStockProductLookupResult(p.ProductId, p.Name, p.QuantityInStock, p.MinimumStock))
            .ToList();
    }
}
