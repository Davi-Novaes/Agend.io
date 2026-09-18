using System.Text.RegularExpressions;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Qual servico mais vendeu?", "servico mais popular" -- ranking por RECEITA
/// (RevenueByService, ja calculado por GetAppointmentStatsQueryHandler).
/// Periodo via PeriodPhraseParser (padrao: mes atual).
/// </summary>
public sealed partial class TopServiceIntentRule(IAppointmentStatsLookupService statsLookup, IClock clock) : IIntentRule
{
    public string IntentId => "TOP_SERVICE";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!ServiceWordPattern().IsMatch(normalizedQuestion) || !TopTriggerPattern().IsMatch(normalizedQuestion))
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

        var stats = await statsLookup.GetStatsAsync(from, to, cancellationToken);
        var top = stats.RevenueByService.OrderByDescending(s => s.Total).FirstOrDefault();

        if (top is null || top.Total == 0)
        {
            return new IntentAnswer($"Nao encontrei vendas de servico entre {from:dd/MM} e {to:dd/MM}.");
        }

        var answer = $"O servico que mais faturou entre {from:dd/MM} e {to:dd/MM} foi \"{top.ServiceName}\", com {top.Total.ToString("C", BrazilianCulture)}.";
        return new IntentAnswer(answer, "/relatorios");
    }

    private static readonly System.Globalization.CultureInfo BrazilianCulture = new("pt-BR");

    [GeneratedRegex(@"\bservicos?\b")]
    private static partial Regex ServiceWordPattern();

    // Sem \b no final: "mais vend" e prefixo de proposito (cobre
    // vendeu/vendido/vendas), um \b ali exigiria a palavra terminar
    // exatamente em "vend", nunca batendo com "vendeu".
    [GeneratedRegex(@"\b(mais vend|mais popular|mais procurado|melhor servico)")]
    private static partial Regex TopTriggerPattern();
}
