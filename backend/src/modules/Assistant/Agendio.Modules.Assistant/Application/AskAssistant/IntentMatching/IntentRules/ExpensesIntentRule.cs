using System.Text.RegularExpressions;
using Agendio.Modules.Financeiro.Contracts;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Quanto gastei esse mes?", "quais foram minhas despesas?" -- SOMENTE o
/// total pago (TotalPaid), distinto de RevenueIntentRule (TotalReceived).
/// Periodo via PeriodPhraseParser (padrao: mes atual).
/// </summary>
public sealed partial class ExpensesIntentRule(IFinanceSummaryLookupService financeLookup, IClock clock) : IIntentRule
{
    public string IntentId => "EXPENSES";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!ExpenseWordPattern().IsMatch(normalizedQuestion))
        {
            return false;
        }

        match = new IntentMatch(IntentId, new Dictionary<string, string> { ["question"] = normalizedQuestion });
        return true;
    }

    public async Task<IntentAnswer> ResolveAsync(IntentMatch match, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var (from, to) = PeriodPhraseParser.Resolve(match.ExtractedArgs["question"], today);

        var summary = await financeLookup.GetCashFlowSummaryAsync(from, to, cancellationToken);
        var periodLabel = from == to ? $"em {from:dd/MM}" : $"entre {from:dd/MM} e {to:dd/MM}";

        var answer = $"Suas despesas {periodLabel} somaram {summary.TotalPaid.ToString("C", BrazilianCulture)}.";
        return new IntentAnswer(answer, "/financeiro");
    }

    private static readonly System.Globalization.CultureInfo BrazilianCulture = new("pt-BR");

    [GeneratedRegex(@"\b(gast|despes)")]
    private static partial Regex ExpenseWordPattern();
}
