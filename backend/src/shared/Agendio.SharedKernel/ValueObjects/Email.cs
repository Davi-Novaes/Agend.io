using System.Text.RegularExpressions;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.ValueObjects;

public sealed partial class Email : ValueObject
{
    // Limite pratico adotado pelas RFCs 5321/5322 para o endereco completo.
    private const int MaxLength = 320;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Email>(Error.Validation("Email.Empty", "O e-mail nao pode ser vazio."));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<Email>(Error.Validation("Email.TooLong", $"O e-mail nao pode ter mais que {MaxLength} caracteres."));
        }

        if (!EmailRegex().IsMatch(normalized))
        {
            return Result.Failure<Email>(Error.Validation("Email.InvalidFormat", "O formato do e-mail e invalido."));
        }

        return Result.Success(new Email(normalized));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    // Validacao pragmatica (nao 100% RFC 5322): suficiente para rejeitar erro de
    // digitacao comum sem reimplementar o parser inteiro do padrao.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
