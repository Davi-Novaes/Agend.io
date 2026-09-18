using System.Text.RegularExpressions;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Qual profissional mais atendeu esse mes?", "ranking de profissional por
/// agendamento" -- ranking por VOLUME (quantidade de atendimentos concluidos),
/// distinto do ranking por receita que so o caminho LLM ja cobria antes.
/// Periodo resolvido por PeriodPhraseParser (padrao: mes atual), "hoje" vem
/// do IClock em UTC -- mesma aproximacao documentada la.
/// </summary>
public sealed partial class ProfessionalRankingIntentRule(IAppointmentStatsLookupService statsLookup, IClock clock) : IIntentRule
{
    public string IntentId => "PROFESSIONAL_RANKING";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!ProfessionalWordPattern().IsMatch(normalizedQuestion) || !RankingTriggerPattern().IsMatch(normalizedQuestion))
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
        var top = stats.CountByProfessional.OrderByDescending(p => p.Count).FirstOrDefault();

        if (top is null || top.Count == 0)
        {
            return new IntentAnswer($"Nao encontrei agendamentos concluidos entre {from:dd/MM} e {to:dd/MM} para montar um ranking.");
        }

        var answer = $"{top.ResourceName} foi quem mais atendeu entre {from:dd/MM} e {to:dd/MM}, com {top.Count} atendimento{(top.Count == 1 ? "" : "s")} concluido{(top.Count == 1 ? "" : "s")}.";
        return new IntentAnswer(answer, "/relatorios");
    }

    [GeneratedRegex(@"\bprofission(al|ais)\b")]
    private static partial Regex ProfessionalWordPattern();

    // Sem \b no final: "mais atend"/"mais agend" sao prefixo de proposito
    // (cobrem atendeu/atendimento, agendou/agendamentos) -- um \b ali nunca
    // bateria com essas formas conjugadas (mesmo bug corrigido em
    // TopServiceIntentRule).
    [GeneratedRegex(@"\b(mais atend|mais agend|ranking|maior volume|quem mais)")]
    private static partial Regex RankingTriggerPattern();
}
