using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.ValueObjects;

/// <summary>
/// Identificador amigavel usado na URL do tenant (ex.: agendio.com.br/e/barbearia-do-ze
/// ou barbearia-do-ze.agendio.com.br). Normaliza acento, maiuscula e espaco.
/// </summary>
public sealed partial class Slug : ValueObject
{
    // Compativel com o limite de um rotulo de subdominio DNS.
    private const int MaxLength = 63;

    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Result<Slug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Slug>(Error.Validation("Slug.Empty", "O identificador nao pode ser vazio."));
        }

        var normalized = Normalize(value);

        if (normalized.Length == 0)
        {
            return Result.Failure<Slug>(Error.Validation("Slug.InvalidFormat", "O identificador precisa conter ao menos uma letra ou numero."));
        }

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<Slug>(Error.Validation("Slug.TooLong", $"O identificador nao pode ter mais que {MaxLength} caracteres."));
        }

        return Result.Success(new Slug(normalized));
    }

    private static string Normalize(string value)
    {
        var withoutDiacritics = RemoveDiacritics(value.Trim().ToLowerInvariant());
        var withDashes = NonAlphanumericRegex().Replace(withoutDiacritics, "-");
        var collapsed = MultipleDashesRegex().Replace(withDashes, "-");
        return collapsed.Trim('-');
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleDashesRegex();
}
