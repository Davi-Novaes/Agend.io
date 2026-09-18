using System.Text.RegularExpressions;
using Agendio.Modules.Estoque.Contracts;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Meu estoque tem algum produto acabando?", "quais produtos estao com
/// estoque baixo?" -- lista (nao so contagem) dos produtos no minimo ou
/// abaixo, foto atual (sem periodo, mesmo criterio de GetInventorySummary).
/// </summary>
public sealed partial class LowStockListIntentRule(IInventorySummaryLookupService inventoryLookup) : IIntentRule
{
    private const int MaxNamesInAnswer = 10;

    public string IntentId => "LOW_STOCK_LIST";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        var mentionsStockOrProduct = StockOrProductPattern().IsMatch(normalizedQuestion);
        var mentionsLowOrRunningOut = LowOrRunningOutPattern().IsMatch(normalizedQuestion);
        if (!mentionsStockOrProduct || !mentionsLowOrRunningOut)
        {
            return false;
        }

        match = new IntentMatch(IntentId, new Dictionary<string, string>());
        return true;
    }

    public async Task<IntentAnswer> ResolveAsync(IntentMatch match, CancellationToken cancellationToken)
    {
        var products = await inventoryLookup.ListLowStockAsync(cancellationToken);

        if (products.Count == 0)
        {
            return new IntentAnswer("Nenhum produto esta com estoque baixo no momento.", "/estoque");
        }

        var names = string.Join(", ", products.Take(MaxNamesInAnswer).Select(p => $"{p.Name} ({p.QuantityInStock})"));
        var suffix = products.Count > MaxNamesInAnswer ? $" e mais {products.Count - MaxNamesInAnswer}" : "";
        var answer = $"{products.Count} produto{(products.Count == 1 ? "" : "s")} com estoque baixo: {names}{suffix}.";

        return new IntentAnswer(answer, "/estoque");
    }

    [GeneratedRegex(@"\b(estoque|produtos?)\b")]
    private static partial Regex StockOrProductPattern();

    [GeneratedRegex(@"\b(baixo|acaba|acabando|acabar|faltando|falta)\b")]
    private static partial Regex LowOrRunningOutPattern();
}
