using System.Text.RegularExpressions;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Quantos agendamentos tenho hoje?", "quantos agendamentos amanha?" --
/// contagem total no periodo (padrao PeriodPhraseParser: mes atual se nao
/// houver frase de periodo). Exige "quant" pra nao competir com
/// ProfessionalRankingIntentRule ("qual profissional mais atendeu").
/// </summary>
public sealed partial class AppointmentCountIntentRule(IAppointmentStatsLookupService statsLookup, IClock clock) : IIntentRule
{
    public string IntentId => "APPOINTMENT_COUNT";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!AppointmentWordPattern().IsMatch(normalizedQuestion) || !CountTriggerPattern().IsMatch(normalizedQuestion))
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
        var periodLabel = from == to
            ? (from == today ? "hoje" : $"em {from:dd/MM}")
            : $"entre {from:dd/MM} e {to:dd/MM}";

        var answer = stats.TotalCount == 1
            ? $"Voce tem 1 agendamento {periodLabel}."
            : $"Voce tem {stats.TotalCount} agendamentos {periodLabel}.";

        return new IntentAnswer(answer, "/agenda");
    }

    [GeneratedRegex(@"\bagendamentos?\b")]
    private static partial Regex AppointmentWordPattern();

    [GeneratedRegex(@"\bquant[oa]s\b")]
    private static partial Regex CountTriggerPattern();
}
