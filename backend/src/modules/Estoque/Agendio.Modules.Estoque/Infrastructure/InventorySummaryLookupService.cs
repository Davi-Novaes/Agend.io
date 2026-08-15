using Agendio.Modules.Estoque.Application.GetInventorySummary;
using Agendio.Modules.Estoque.Contracts;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Infrastructure;

// Delega para o handler ja existente via IDispatcher (chamada intra-modulo,
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
}
