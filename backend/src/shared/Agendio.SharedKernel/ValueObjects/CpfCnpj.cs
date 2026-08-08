using System.Text.RegularExpressions;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.ValueObjects;

/// <summary>CPF (11 digitos) ou CNPJ (14 digitos), com digito verificador validado (modulo 11).</summary>
public sealed partial class CpfCnpj : ValueObject
{
    public string Value { get; }

    private CpfCnpj(string value) => Value = value;

    public static Result<CpfCnpj> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<CpfCnpj>(Error.Validation("CpfCnpj.Empty", "O CPF/CNPJ nao pode ser vazio."));
        }

        var digitsOnly = NonDigitRegex().Replace(value, string.Empty);

        var isValid = digitsOnly.Length switch
        {
            11 => IsValidCpf(digitsOnly),
            14 => IsValidCnpj(digitsOnly),
            _ => false,
        };

        if (!isValid)
        {
            return Result.Failure<CpfCnpj>(Error.Validation("CpfCnpj.InvalidFormat", "O CPF/CNPJ informado e invalido."));
        }

        return Result.Success(new CpfCnpj(digitsOnly));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    private static bool IsValidCpf(string digits)
    {
        if (AllDigitsEqual(digits))
        {
            return false;
        }

        var firstCheckDigit = CalculateCheckDigit(digits, 9, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var secondCheckDigit = CalculateCheckDigit(digits, 10, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);

        return digits[9] - '0' == firstCheckDigit && digits[10] - '0' == secondCheckDigit;
    }

    private static bool IsValidCnpj(string digits)
    {
        if (AllDigitsEqual(digits))
        {
            return false;
        }

        var firstCheckDigit = CalculateCheckDigit(digits, 12, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var secondCheckDigit = CalculateCheckDigit(digits, 13, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);

        return digits[12] - '0' == firstCheckDigit && digits[13] - '0' == secondCheckDigit;
    }

    private static int CalculateCheckDigit(string digits, int digitCount, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < digitCount; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static bool AllDigitsEqual(string digits) => digits.Distinct().Count() == 1;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();
}
