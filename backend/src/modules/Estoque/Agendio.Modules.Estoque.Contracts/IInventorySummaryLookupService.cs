namespace Agendio.Modules.Estoque.Contracts;

/// <summary>
/// Foto do estoque atual — usada pelo Assistente (Fase 22) para responder
/// perguntas em linguagem natural sem o modulo Assistant precisar ler tabela do
/// Estoque diretamente. Sem parametros de proposito, mesma razao de
/// GetInventorySummaryQuery: e uma foto do estoque atual, nao um historico.
/// </summary>
public interface IInventorySummaryLookupService
{
    Task<InventorySummaryLookupResult> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed record StockValueByCurrencyLookup(string Currency, decimal Total);

public sealed record InventorySummaryLookupResult(
    int ActiveProductCount, int LowStockCount, IReadOnlyList<StockValueByCurrencyLookup> TotalStockValue);
