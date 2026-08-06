using System.Text.RegularExpressions;

namespace Agendio.Modules.Tenancy.Domain;

/// <summary>
/// Formula de contraste do WCAG 2.1 (relative luminance + contrast ratio).
/// Existe porque "Paleta de tema personalizada e rejeitada ao salvar se nao
/// atingir contraste AA" e regra de negocio explicita do projeto, nao
/// preferencia de UI — falha aqui bloqueia o comando, nao so um aviso visual.
/// </summary>
public static partial class ContrastValidator
{
    // 4.5:1 e o minimo AA para texto normal (WCAG 2.1, criterio 1.4.3).
    private const double MinimumAAContrastRatio = 4.5;

    public static bool IsValidHexColor(string? hex) => hex is not null && HexColorRegex().IsMatch(hex);

    public static bool MeetsAaContrast(string foregroundHex, string backgroundHex) =>
        ContrastRatio(foregroundHex, backgroundHex) >= MinimumAAContrastRatio;

    public static double ContrastRatio(string hexA, string hexB)
    {
        var luminanceA = RelativeLuminance(hexA);
        var luminanceB = RelativeLuminance(hexB);

        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var (r, g, b) = ParseRgb(hex);

        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    private static double Linearize(double channel) =>
        channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static (double R, double G, double B) ParseRgb(string hex)
    {
        var value = hex.TrimStart('#');
        var r = Convert.ToInt32(value[..2], 16) / 255.0;
        var g = Convert.ToInt32(value[2..4], 16) / 255.0;
        var b = Convert.ToInt32(value[4..6], 16) / 255.0;
        return (r, g, b);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();
}
