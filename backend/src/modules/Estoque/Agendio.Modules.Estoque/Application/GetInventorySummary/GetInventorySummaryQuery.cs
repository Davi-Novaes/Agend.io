using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.GetInventorySummary;

// Sem parametros de proposito: e uma foto do estoque atual, nao um historico.
public sealed record GetInventorySummaryQuery : IQuery<InventorySummary>;

public sealed record StockValueByCurrency(string Currency, decimal Total);

public sealed record InventorySummary(int ActiveProductCount, int LowStockCount, IReadOnlyList<StockValueByCurrency> TotalStockValue);
