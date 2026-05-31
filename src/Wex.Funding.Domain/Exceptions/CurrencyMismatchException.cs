namespace Wex.Funding.Domain.Exceptions;

public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(string left, string right)
        : base($"Currency mismatch: cannot operate on {left} and {right}.") { }
}
