using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.SharedKernel.ValueObjects;

/// <summary>
/// Valor monetario com moeda (ISO 4217). Preco de servico, comissao e valor de
/// fatura sao Money, nunca decimal solto — impede somar Real com Dolar sem querer.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }

    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency = "BRL")
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(Error.Validation("Money.Negative", "O valor monetario nao pode ser negativo."));
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return Result.Failure<Money>(Error.Validation("Money.InvalidCurrency", "A moeda deve seguir o formato ISO 4217 (ex.: BRL, USD)."));
        }

        return Result.Success(new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), currency.ToUpperInvariant()));
    }

    public static Money Zero(string currency = "BRL") => new(0m, currency.ToUpperInvariant());

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Nao e possivel operar valores em moedas diferentes ({Currency} e {other.Currency}).");
        }
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
