using System.Text.RegularExpressions;
using Agendio.Modules.Financeiro.Contracts;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Quanto faturei esse mes?", "faturamento de hoje", "quanto faturei mes
/// passado?" -- SOMENTE o total recebido (TotalReceived), distinto de
/// ExpensesIntentRule (TotalPaid). Periodo via PeriodPhraseParser (padrao: mes
/// atual).
/// </summary>
public sealed partial class RevenueIntentRule(IFinanceSummaryLookupService financeLookup, IClock clock) : IIntentRule
{
    public string IntentId => "REVENUE";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!RevenueWordPattern().IsMatch(normalizedQuestion))
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

        var answer = $"Seu faturamento {periodLabel} foi de {summary.TotalReceived.ToString("C", BrazilianCulture)}.";
        return new IntentAnswer(answer, "/financeiro");
    }

    private static readonly System.Globalization.CultureInfo BrazilianCulture = new("pt-BR");

    // "fatur" cobre faturei/faturamento/fatura -- nao exige palavra de
    // periodo (PeriodPhraseParser ja cai em "mes atual" por padrao, mesmo
    // default que o prompt do LLM ja usava).
    [GeneratedRegex(@"\bfatur")]
    private static partial Regex RevenueWordPattern();
}
