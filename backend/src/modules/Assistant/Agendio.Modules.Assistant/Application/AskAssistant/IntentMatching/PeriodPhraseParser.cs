using System.Text.RegularExpressions;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;

/// <summary>
/// Resolve frases de periodo comuns ("hoje", "essa semana", "mes passado"
/// etc.) pro par (From, To) que as tools/lookup services ja esperam -- mesma
/// semantica que o system prompt do LLM ja usa hoje ("quando o periodo nao
/// for explicito, calcule a partir de hoje"), pra fast-path e LLM nunca
/// divergirem no periodo assumido. `today` vem em UTC (IClock), nao no fuso
/// do tenant -- aproximacao aceita de proposito pra nao pagar uma consulta a
/// mais (ITenantLookupService) so pra resolver o periodo no fast-path; o
/// pior caso e um dia de diferenca perto da virada de mes/semana, e quem
/// quiser precisao total sempre pode reformular a pergunta pro caminho LLM
/// (que ja usa o fuso certo).
/// </summary>
public static partial class PeriodPhraseParser
{
    // Nomes em portugues, indice 0 = janeiro -- normalizado (sem acento, ver
    // TextNormalization) porque "normalizedQuestion" ja chega sem acento.
    private static readonly string[] MonthNames =
    [
        "janeiro", "fevereiro", "marco", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
    ];
    public static (DateOnly From, DateOnly To) Resolve(string normalizedQuestion, DateOnly today)
    {
        if (normalizedQuestion.Contains("hoje", StringComparison.Ordinal))
        {
            return (today, today);
        }

        if (normalizedQuestion.Contains("ontem", StringComparison.Ordinal))
        {
            var yesterday = today.AddDays(-1);
            return (yesterday, yesterday);
        }

        if (normalizedQuestion.Contains("amanha", StringComparison.Ordinal))
        {
            var tomorrow = today.AddDays(1);
            return (tomorrow, tomorrow);
        }

        if (normalizedQuestion.Contains("semana passada", StringComparison.Ordinal))
        {
            var lastWeekEnd = StartOfWeek(today).AddDays(-1);
            return (StartOfWeek(lastWeekEnd), lastWeekEnd);
        }

        if (Contains(normalizedQuestion, "essa semana", "esta semana"))
        {
            return (StartOfWeek(today), today);
        }

        if (normalizedQuestion.Contains("mes passado", StringComparison.Ordinal))
        {
            var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
            var lastMonthEnd = firstOfThisMonth.AddDays(-1);
            return (new DateOnly(lastMonthEnd.Year, lastMonthEnd.Month, 1), lastMonthEnd);
        }

        if (Contains(normalizedQuestion, "esse ano", "este ano"))
        {
            return (new DateOnly(today.Year, 1, 1), today);
        }

        if (Contains(normalizedQuestion, "ultimos 30 dias", "ultimos trinta dias"))
        {
            return (today.AddDays(-30), today);
        }

        // Mes explicito por nome ("faturamento de agosto") ou numero ("mes 8",
        // "mes 08") -- ano assumido e o atual, EXCETO se esse mes ainda nao
        // comecou este ano (ex.: perguntar por "dezembro" em marco), caso em
        // que assume o ano passado (mesma heuristica de "sempre um periodo que
        // ja aconteceu", nunca no futuro).
        var explicitMonth = TryResolveExplicitMonth(normalizedQuestion);
        if (explicitMonth is { } month)
        {
            var year = month > today.Month ? today.Year - 1 : today.Year;
            var firstOfMonth = new DateOnly(year, month, 1);
            var isCurrentMonth = year == today.Year && month == today.Month;
            return (firstOfMonth, isCurrentMonth ? today : firstOfMonth.AddMonths(1).AddDays(-1));
        }

        // Padrao: mes atual -- mesmo default do prompt do LLM.
        return (new DateOnly(today.Year, today.Month, 1), today);
    }

    private static int? TryResolveExplicitMonth(string normalizedQuestion)
    {
        for (var i = 0; i < MonthNames.Length; i++)
        {
            if (normalizedQuestion.Contains(MonthNames[i], StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        var numericMatch = NumericMonthPattern().Match(normalizedQuestion);
        if (numericMatch.Success && int.TryParse(numericMatch.Groups[1].Value, out var monthNumber) && monthNumber is >= 1 and <= 12)
        {
            return monthNumber;
        }

        return null;
    }

    private static bool Contains(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff);
    }

    // "mes 8", "mes 08", "mes08" -- nao "mes 8 e meio" nem outras variacoes
    // improvaveis, so o caso conservador (numero de 1-2 digitos logo apos "mes").
    [GeneratedRegex(@"\bmes\s*(\d{1,2})\b")]
    private static partial Regex NumericMonthPattern();
}
