namespace Wex.Funding.Domain.Exceptions;

public sealed class InvalidCurrencyException : DomainException
{
    public InvalidCurrencyException(string value)
        : base($"'{value}' is not a valid ISO 4217 currency code.") { }
}
