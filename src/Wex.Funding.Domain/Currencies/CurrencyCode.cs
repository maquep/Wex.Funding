using System.Text.RegularExpressions;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Currencies;

public readonly record struct CurrencyCode
{
    private static readonly Regex _isoPattern = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public string Value { get; }

    public CurrencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !_isoPattern.IsMatch(value))
        {
            throw new InvalidCurrencyException(value ?? string.Empty);
        }

        Value = value;
    }

    public override string ToString() => Value;
}
