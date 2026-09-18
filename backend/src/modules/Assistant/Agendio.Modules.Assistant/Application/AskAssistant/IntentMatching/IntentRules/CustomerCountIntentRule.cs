using System.Text.RegularExpressions;
using Agendio.Modules.Customers.Contracts;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Quantos clientes eu tenho?", "quantos clientes estao cadastrados?" etc.
/// So contagem agregada (ativos/inativos) -- nunca dado de um cliente
/// especifico, mesma restricao que o prompt do LLM ja aplica.
/// </summary>
public sealed partial class CustomerCountIntentRule(ICustomerDirectoryLookupService directoryLookup) : IIntentRule
{
    public string IntentId => "CUSTOMER_COUNT";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!CustomerCountPattern().IsMatch(normalizedQuestion))
        {
            return false;
        }

        match = new IntentMatch(IntentId, new Dictionary<string, string>());
        return true;
    }

    public async Task<IntentAnswer> ResolveAsync(IntentMatch match, CancellationToken cancellationToken)
    {
        var summary = await directoryLookup.GetSummaryAsync(cancellationToken);
        var answer = summary.TotalActiveCount == 1
            ? "Voce tem 1 cliente ativo cadastrado."
            : $"Voce tem {summary.TotalActiveCount} clientes ativos cadastrados.";

        if (summary.TotalInactiveCount > 0)
        {
            answer += $" ({summary.TotalInactiveCount} inativo{(summary.TotalInactiveCount == 1 ? "" : "s")}.)";
        }

        return new IntentAnswer(answer, "/clientes");
    }

    // Exige "quant[oa]s" e "client" na mesma pergunta -- "clientes" cobre
    // singular/plural, "quantos/quantas" cobre genero. Nao exige palavra
    // extra tipo "tenho"/"cadastrados" pra cobrir as variacoes do pedido
    // original ("quantos clientes tenho", "quantos clientes estao
    // cadastrados", "me mostra a quantidade de clientes" cai no LLM, que
    // tambem sabe responder isso).
    [GeneratedRegex(@"\bquant[oa]s\b.*\bclientes?\b")]
    private static partial Regex CustomerCountPattern();
}
