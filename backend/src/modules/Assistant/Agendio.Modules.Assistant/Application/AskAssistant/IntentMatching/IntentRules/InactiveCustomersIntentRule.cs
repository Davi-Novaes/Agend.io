using System.Globalization;
using System.Text.RegularExpressions;
using Agendio.Modules.Customers.Contracts;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching.IntentRules;

/// <summary>
/// "Clientes inativos ha 60 dias", "clientes que nao agendam ha mais de 60
/// dias" -- corte de dias fixo (extraido da pergunta, ou 60 por padrao, o
/// mesmo numero do exemplo do proprio pedido original). So conta e lista
/// nomes -- nunca detalhe de contato (telefone/e-mail), que fica reservado
/// pro caminho LLM decidir se e apropriado revelar.
/// </summary>
public sealed partial class InactiveCustomersIntentRule(ICustomerLookupService customerLookup) : IIntentRule
{
    private const int DefaultDays = 60;
    private const int MaxNamesInAnswer = 10;

    public string IntentId => "INACTIVE_CUSTOMERS";

    public bool TryMatch(string normalizedQuestion, string callerRole, out IntentMatch match)
    {
        match = null!;
        if (!InactiveTriggerPattern().IsMatch(normalizedQuestion) || !CustomerWordPattern().IsMatch(normalizedQuestion))
        {
            return false;
        }

        var daysMatch = DaysPattern().Match(normalizedQuestion);
        var days = daysMatch.Success ? int.Parse(daysMatch.Groups[1].Value, CultureInfo.InvariantCulture) : DefaultDays;

        match = new IntentMatch(IntentId, new Dictionary<string, string> { ["days"] = days.ToString(CultureInfo.InvariantCulture) });
        return true;
    }

    public async Task<IntentAnswer> ResolveAsync(IntentMatch match, CancellationToken cancellationToken)
    {
        var days = int.Parse(match.ExtractedArgs["days"], CultureInfo.InvariantCulture);
        var customers = await customerLookup.ListInactiveSinceAsync(days, cancellationToken);

        if (customers.Count == 0)
        {
            return new IntentAnswer($"Nenhum cliente ativo esta sem contato ha {days} dias ou mais.");
        }

        var names = string.Join(", ", customers.Take(MaxNamesInAnswer).Select(c => c.FullName));
        var suffix = customers.Count > MaxNamesInAnswer ? $" e mais {customers.Count - MaxNamesInAnswer}" : "";
        var answer = $"{customers.Count} cliente{(customers.Count == 1 ? "" : "s")} sem contato ha {days} dias ou mais: {names}{suffix}.";

        return new IntentAnswer(answer, "/clientes");
    }

    [GeneratedRegex(@"\b(inativ|nao agend|sem contato|nao volta)")]
    private static partial Regex InactiveTriggerPattern();

    [GeneratedRegex(@"\bclientes?\b")]
    private static partial Regex CustomerWordPattern();

    [GeneratedRegex(@"(\d+)\s*dias")]
    private static partial Regex DaysPattern();
}
