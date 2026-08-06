using System.Text.RegularExpressions;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.ValueObjects;

/// <summary>Telefone normalizado em E.164 (ex.: +5511999998888), usado para WhatsApp/SMS.</summary>
public sealed partial class PhoneNumber : ValueObject
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static Result<PhoneNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PhoneNumber>(Error.Validation("PhoneNumber.Empty", "O telefone nao pode ser vazio."));
        }

        var digitsOnly = NonDigitRegex().Replace(value, string.Empty);

        // Numero sem DDI: assume Brasil (+55), o caso comum para os tenants iniciais.
        if (digitsOnly.Length is 10 or 11)
        {
            digitsOnly = "55" + digitsOnly;
        }

        if (digitsOnly.Length is < 11 or > 15)
        {
            return Result.Failure<PhoneNumber>(Error.Validation("PhoneNumber.InvalidFormat", "O telefone informado e invalido."));
        }

        return Result.Success(new PhoneNumber("+" + digitsOnly));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();
}
