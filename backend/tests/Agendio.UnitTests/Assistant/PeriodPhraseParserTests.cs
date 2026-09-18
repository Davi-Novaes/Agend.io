using Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;

namespace Agendio.UnitTests.Assistant;

/// <summary>
/// Bug real reportado em producao: "qual meu faturamento no mes 8" respondia
/// o mesmo que "qual meu faturamento" (mes atual), porque PeriodPhraseParser
/// so reconhecia frases relativas (hoje/mes passado/etc.), nunca mes
/// explicito por nome ou numero -- cala no fallback de "mes atual" sem avisar.
/// </summary>
public class PeriodPhraseParserTests
{
    private static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void Explicit_Numeric_Month_In_The_Past_Should_Resolve_To_That_Month_This_Year()
    {
        var (from, to) = PeriodPhraseParser.Resolve("qual meu faturamento no mes 8", Today);

        from.ShouldBe(new DateOnly(2026, 8, 1));
        to.ShouldBe(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void Explicit_Month_Name_In_The_Past_Should_Resolve_To_That_Month_This_Year()
    {
        var (from, to) = PeriodPhraseParser.Resolve("quanto faturei em agosto?", Today);

        from.ShouldBe(new DateOnly(2026, 8, 1));
        to.ShouldBe(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void Explicit_Month_Number_Greater_Than_Current_Month_Should_Resolve_To_Last_Year()
    {
        // Hoje e setembro/2026 -- perguntar por "mes 12" (dezembro) so pode se
        // referir a dezembro/2025 (dezembro/2026 ainda nao aconteceu).
        var (from, to) = PeriodPhraseParser.Resolve("faturamento do mes 12", Today);

        from.ShouldBe(new DateOnly(2025, 12, 1));
        to.ShouldBe(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Explicit_Current_Month_Should_Resolve_Up_To_Today_Not_The_Full_Month()
    {
        var (from, to) = PeriodPhraseParser.Resolve("faturamento do mes 9", Today);

        from.ShouldBe(new DateOnly(2026, 9, 1));
        to.ShouldBe(Today);
    }

    [Fact]
    public void Relative_Phrases_Still_Take_Priority_Over_Explicit_Month_Parsing()
    {
        // "mes passado" nao pode ser interpretado como "mes" + numero nenhum
        // -- checagem de frase relativa roda ANTES da checagem de mes explicito.
        var (from, to) = PeriodPhraseParser.Resolve("faturamento do mes passado", Today);

        from.ShouldBe(new DateOnly(2026, 8, 1));
        to.ShouldBe(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void No_Period_Phrase_At_All_Should_Still_Default_To_Current_Month_To_Date()
    {
        var (from, to) = PeriodPhraseParser.Resolve("qual meu faturamento?", Today);

        from.ShouldBe(new DateOnly(2026, 9, 1));
        to.ShouldBe(Today);
    }
}
