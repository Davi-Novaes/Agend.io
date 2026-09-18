using System.Globalization;
using System.Text;

namespace Agendio.Modules.Assistant.Application.AskAssistant.IntentMatching;

/// <summary>
/// Normalizacao de texto compartilhada por todo IIntentRule e pela busca do
/// FeatureCatalog -- minusculo + sem acento, pra "clientes", "Clientes" e
/// "clíentes" (erro de digitacao comum) caírem na mesma forma comparavel.
/// Regex simples, sem stemming/NLP: mantem o fast-path barato e previsivel.
/// </summary>
public static class TextNormalization
{
    public static string Normalize(string value)
    {
        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
