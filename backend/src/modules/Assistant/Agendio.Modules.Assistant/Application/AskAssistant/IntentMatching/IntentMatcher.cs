namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;

/// <summary>Resultado de uma tentativa de fast-path -- Answer preenchido so quando Matched.</summary>
public sealed record IntentMatchResult(bool Matched, string? IntentId, string? Answer, string? SuggestedRoute = null);

/// <summary>
/// Orquestra o fast-path: tenta cada IIntentRule registrada (ordem de
/// registro no DI = ordem de tentativa -- regras mais especificas primeiro),
/// a primeira que reconhecer a pergunta resolve a resposta direto contra o
/// Lookup Service correspondente, sem chamar IA nenhuma. Nenhum match =
/// AskAssistantCommandHandler cai no loop de tool-calling existente, sem
/// nenhuma mudanca de comportamento.
/// </summary>
public sealed class IntentMatcher(IEnumerable<IIntentRule> rules)
{
    public async Task<IntentMatchResult> TryResolveAsync(string question, string callerRole, CancellationToken cancellationToken)
    {
        var normalized = TextNormalization.Normalize(question);

        foreach (var rule in rules)
        {
            if (rule.TryMatch(normalized, callerRole, out var match))
            {
                var answer = await rule.ResolveAsync(match, cancellationToken);
                return new IntentMatchResult(true, rule.IntentId, answer.Text, answer.SuggestedRoute);
            }
        }

        return new IntentMatchResult(false, null, null);
    }
}
