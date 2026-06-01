using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Currencies;

public readonly record struct Money
{
    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

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
            throw new CurrencyMismatchException(Currency.Value, other.Currency.Value);
        }
    }

    public override string ToString() => $"{Amount:F4} {Currency}";
}
