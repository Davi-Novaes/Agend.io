using System.Text.RegularExpressions;
using Agendio.Modules.Assistant.Knowledge;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// Reconhece pergunta de navegacao/"como eu faco" ("como cadastro um
/// funcionario", "onde vejo meus clientes") e responde com a rota real e as
/// capacidades daquela tela, direto do FeatureCatalog -- sem chamar nenhum
/// Lookup Service nem IA. So casa quando ha um gatilho de navegacao claro
/// ("como"/"onde"/"aonde") E o catalogo acha uma tela com pelo menos 2 termos
/// batendo (minScore: 2) -- exige mais confianca que a busca usada so pra
/// enriquecer o prompt do LLM, porque aqui a resposta e definitiva, sem o
/// modelo pra suavizar um match fraco.
/// </summary>
public sealed partial class NavigationIntentRule : IIntentRule
{
    public string IntentId => "NAVIGATION_HELP";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!NavigationTriggerPattern().IsMatch(normalizedQuestion))
        {
            return false;
        }

        // Filtra pelo papel ANTES de pontuar -- Staff nunca recebe orientacao de
        // navegacao para uma tela que ele nem consegue abrir (ex.: cancelar
        // assinatura, Owner-only). Nesse caso a pergunta simplesmente nao casa
        // aqui e cai pro LLM, que ja sabe recusar educadamente perguntas fora
        // do escopo (mesmo padrao usado pra dado fora do escopo hoje).
        var candidates = FeatureCatalog.ForRole(callerRole);
        var results = FeatureCatalog.Search(candidates, normalizedQuestion, maxResults: 1, minScore: 2);
        if (results.Count == 0)
        {
            return false;
        }

        match = new IntentMatch(IntentId, new Dictionary<string, string> { ["featureId"] = results[0].Id });
        return true;
    }

    public Task<IntentAnswer> ResolveAsync(IntentMatch match, CancellationToken cancellationToken)
    {
        var feature = FeatureCatalog.ById(match.ExtractedArgs["featureId"])
            ?? throw new InvalidOperationException($"Feature '{match.ExtractedArgs["featureId"]}' nao encontrada no catalogo.");

        var answer = $"{feature.Description} Acesse em \"{feature.Name}\".";
        if (feature.Capabilities.Count > 0)
        {
            answer += $" Por la voce pode: {string.Join(", ", feature.Capabilities)}.";
        }

        return Task.FromResult(new IntentAnswer(answer, feature.Route));
    }

    [GeneratedRegex(@"\b(como|onde|aonde)\b")]
    private static partial Regex NavigationTriggerPattern();
}
